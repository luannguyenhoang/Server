using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using BCrypt.Net;

namespace HoodLab.Api.Services;

public class DbInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public DbInitializer(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        try
        {
            Console.WriteLine("🔄 Đang kiểm tra database...");
            
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                Console.WriteLine("⚠️  Không thể kết nối database.");
                throw new Exception("Không thể kết nối database. Vui lòng kiểm tra connection string.");
            }

            Console.WriteLine("✅ Đã kết nối database. Đang kiểm tra tables...");

            var usersTableExists = await TableExistsAsync();

            if (!usersTableExists)
            {
                Console.WriteLine("📦 Đang tạo tables...");
                try
                {
                    await _context.Database.EnsureCreatedAsync();
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  EnsureCreated lỗi: {ex.Message}");
                    Console.WriteLine("📦 Đang thử tạo tables bằng SQL trực tiếp...");
                    await CreateTablesManuallyAsync();
                }
                
                var verifyTableExists = await TableExistsAsync();
                if (!verifyTableExists)
                {
                    Console.WriteLine("⚠️  Tables vẫn chưa tồn tại. Đang thử tạo bằng SQL...");
                    await CreateTablesManuallyAsync();
                }
                
                Console.WriteLine("✅ Đã tạo tables thành công!");
            }
            else
            {
                Console.WriteLine("ℹ️  Tables đã tồn tại.");
                // Luôn chạy migration để đảm bảo cấu trúc đúng
                await MigrateExistingDataAsync();
            }

            await SeedAdminAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khởi tạo database: {ex.Message}");
            Console.WriteLine($"   Chi tiết: {ex.InnerException?.Message ?? ex.ToString()}");
            throw;
        }
    }

    private async Task<bool> TableExistsAsync()
    {
        try
        {
            var sql = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'Users'";
            var result = await _context.Database.ExecuteSqlRawAsync(sql);
            return result > 0;
        }
        catch
        {
            try
            {
                await _context.Users.CountAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private async Task CreateTablesManuallyAsync()
    {
        try
        {
            Console.WriteLine("📦 Đang tạo tất cả tables...");

            var createUsersTable = @"
                CREATE TABLE IF NOT EXISTS `Users` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Email` varchar(255) NOT NULL,
                    `PasswordHash` longtext NOT NULL,
                    `FullName` varchar(255) NOT NULL,
                    `Phone` varchar(255) NOT NULL,
                    `Address` varchar(255) NOT NULL,
                    `Role` varchar(255) NOT NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_Users_Email` (`Email`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createCategoriesTable = @"
                CREATE TABLE IF NOT EXISTS `Categories` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Name` varchar(255) NOT NULL,
                    `Description` longtext NULL,
                    `ImageUrl` varchar(500) NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createBrandsTable = @"
                CREATE TABLE IF NOT EXISTS `Brands` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Name` varchar(255) NOT NULL,
                    `Description` longtext NULL,
                    `LogoUrl` varchar(500) NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createColorsTable = @"
                CREATE TABLE IF NOT EXISTS `Colors` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Name` varchar(255) NOT NULL,
                    `HexCode` varchar(50) NOT NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createSizesTable = @"
                CREATE TABLE IF NOT EXISTS `Sizes` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Name` varchar(255) NOT NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createProductsTable = @"
                CREATE TABLE IF NOT EXISTS `Products` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Name` varchar(255) NOT NULL,
                    `Description` longtext NULL,
                    `Price` decimal(18,2) NOT NULL,
                    `SalePrice` decimal(18,2) NULL,
                    `CategoryId` int NOT NULL,
                    `BrandId` int NOT NULL,
                    `Stock` int NOT NULL,
                    `ImageUrl` varchar(500) NULL,
                    `ImageUrls` longtext NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_Products_CategoryId` (`CategoryId`),
                    KEY `IX_Products_BrandId` (`BrandId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createProductVariantsTable = @"
                CREATE TABLE IF NOT EXISTS `ProductVariants` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ProductId` int NOT NULL,
                    `ColorId` int NOT NULL,
                    `ImageUrl` varchar(500) NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_ProductVariants_ProductId_ColorId` (`ProductId`, `ColorId`),
                    KEY `IX_ProductVariants_ProductId` (`ProductId`),
                    KEY `IX_ProductVariants_ColorId` (`ColorId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createProductVariantSizesTable = @"
                CREATE TABLE IF NOT EXISTS `ProductVariantSizes` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ProductVariantId` int NOT NULL,
                    `SizeId` int NOT NULL,
                    `Stock` int NOT NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_ProductVariantSizes_ProductVariantId_SizeId` (`ProductVariantId`, `SizeId`),
                    KEY `IX_ProductVariantSizes_ProductVariantId` (`ProductVariantId`),
                    KEY `IX_ProductVariantSizes_SizeId` (`SizeId`),
                    CONSTRAINT `FK_ProductVariantSizes_ProductVariants_ProductVariantId` 
                        FOREIGN KEY (`ProductVariantId`) REFERENCES `ProductVariants` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_ProductVariantSizes_Sizes_SizeId` 
                        FOREIGN KEY (`SizeId`) REFERENCES `Sizes` (`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createCartsTable = @"
                CREATE TABLE IF NOT EXISTS `Carts` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `UserId` int NOT NULL,
                    `ProductVariantId` int NOT NULL,
                    `SizeId` int NOT NULL,
                    `Quantity` int NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_Carts_UserId_ProductVariantId_SizeId` (`UserId`, `ProductVariantId`, `SizeId`),
                    KEY `IX_Carts_UserId` (`UserId`),
                    KEY `IX_Carts_ProductVariantId` (`ProductVariantId`),
                    KEY `IX_Carts_SizeId` (`SizeId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createOrdersTable = @"
                CREATE TABLE IF NOT EXISTS `Orders` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `OrderNumber` varchar(255) NOT NULL,
                    `UserId` int NOT NULL,
                    `TotalAmount` decimal(18,2) NOT NULL,
                    `PaymentMethod` varchar(255) NOT NULL,
                    `PaymentStatus` varchar(255) NOT NULL,
                    `OrderStatus` varchar(255) NOT NULL,
                    `ShippingAddress` varchar(500) NULL,
                    `Phone` varchar(255) NULL,
                    `Notes` longtext NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_Orders_UserId` (`UserId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createOrderItemsTable = @"
                CREATE TABLE IF NOT EXISTS `OrderItems` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `OrderId` int NOT NULL,
                    `ProductId` int NOT NULL,
                    `ProductVariantId` int NOT NULL,
                    `SizeId` int NOT NULL,
                    `ProductName` varchar(255) NOT NULL,
                    `ColorName` varchar(255) NOT NULL,
                    `SizeName` varchar(255) NOT NULL,
                    `Price` decimal(18,2) NOT NULL,
                    `Quantity` int NOT NULL,
                    `SubTotal` decimal(18,2) NOT NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_OrderItems_OrderId` (`OrderId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createReviewsTable = @"
                CREATE TABLE IF NOT EXISTS `Reviews` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ProductId` int NOT NULL,
                    `UserId` int NOT NULL,
                    `OrderId` int NOT NULL,
                    `OrderItemId` int NOT NULL,
                    `Rating` int NOT NULL,
                    `Comment` longtext NULL,
                    `ImageUrls` json NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_Reviews_ProductId` (`ProductId`),
                    KEY `IX_Reviews_UserId` (`UserId`),
                    KEY `IX_Reviews_OrderId` (`OrderId`),
                    KEY `IX_Reviews_OrderItemId` (`OrderItemId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createSlidersTable = @"
                CREATE TABLE IF NOT EXISTS `Sliders` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ImageUrl` varchar(500) NOT NULL,
                    `DisplayOrder` int NOT NULL DEFAULT 0,
                    `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createNewsTable = @"
                CREATE TABLE IF NOT EXISTS `News` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Title` varchar(255) NOT NULL,
                    `Excerpt` text NOT NULL,
                    `Content` longtext NULL,
                    `ImageUrl` varchar(500) NULL,
                    `Category` varchar(100) NULL,
                    `PublishedAt` datetime(6) NOT NULL,
                    `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            var createPasswordResetTokensTable = @"
                CREATE TABLE IF NOT EXISTS `PasswordResetTokens` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `UserId` int NOT NULL,
                    `Token` varchar(255) NOT NULL,
                    `ExpiresAt` datetime(6) NOT NULL,
                    `IsUsed` tinyint(1) NOT NULL DEFAULT 0,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_PasswordResetTokens_Token` (`Token`),
                    KEY `IX_PasswordResetTokens_UserId` (`UserId`),
                    CONSTRAINT `FK_PasswordResetTokens_Users_UserId` 
                        FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await _context.Database.ExecuteSqlRawAsync(createUsersTable);
            await _context.Database.ExecuteSqlRawAsync(createCategoriesTable);
            await _context.Database.ExecuteSqlRawAsync(createBrandsTable);
            await _context.Database.ExecuteSqlRawAsync(createColorsTable);
            await _context.Database.ExecuteSqlRawAsync(createSizesTable);
            await _context.Database.ExecuteSqlRawAsync(createProductsTable);
            await _context.Database.ExecuteSqlRawAsync(createProductVariantsTable);
            await _context.Database.ExecuteSqlRawAsync(createProductVariantSizesTable);
            await _context.Database.ExecuteSqlRawAsync(createCartsTable);
            await _context.Database.ExecuteSqlRawAsync(createOrdersTable);
            await _context.Database.ExecuteSqlRawAsync(createOrderItemsTable);
            await _context.Database.ExecuteSqlRawAsync(createReviewsTable);
            await _context.Database.ExecuteSqlRawAsync(createSlidersTable);
            await _context.Database.ExecuteSqlRawAsync(createNewsTable);
            await _context.Database.ExecuteSqlRawAsync(createPasswordResetTokensTable);
            
            // Migrate existing data if needed
            await MigrateExistingDataAsync();

            Console.WriteLine("✅ Đã tạo tất cả tables thành công!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Lỗi tạo tables thủ công: {ex.Message}");
            throw;
        }
    }

    private async Task SeedAdminAsync()
    {
        var adminEmail = _configuration["Admin:Email"] ?? "admin@hoodlab.com";
        var adminPassword = _configuration["Admin:Password"] ?? "Admin@123";
        var adminFullName = _configuration["Admin:FullName"] ?? "Administrator";
        var adminPhone = _configuration["Admin:Phone"] ?? "0123456789";
        var adminAddress = _configuration["Admin:Address"] ?? "HoodLab Office";

        var existingAdmin = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (existingAdmin == null)
        {
            var admin = new User
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                FullName = adminFullName,
                Phone = adminPhone,
                Address = adminAddress,
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(admin);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Đã tạo tài khoản Admin:");
            Console.WriteLine($"   Email: {adminEmail}");
            Console.WriteLine($"   Password: {adminPassword}");
        }
        else
        {
            Console.WriteLine($"ℹ️  Tài khoản Admin đã tồn tại: {adminEmail}");
        }
    }

    private async Task MigrateExistingDataAsync()
    {
        try
        {
            Console.WriteLine("🔄 Đang kiểm tra và migrate cấu trúc database...");
            
            // Kiểm tra và tạo bảng Reviews nếu chưa có
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'Reviews'";
                var result = await checkCommand.ExecuteScalarAsync();
                var reviewsExists = Convert.ToInt32(result) > 0;
                
                if (!reviewsExists)
                {
                    Console.WriteLine("📦 Đang tạo bảng Reviews...");
                    var createReviewsTable = @"
                        CREATE TABLE IF NOT EXISTS `Reviews` (
                            `Id` int NOT NULL AUTO_INCREMENT,
                            `ProductId` int NOT NULL,
                            `UserId` int NOT NULL,
                            `OrderId` int NOT NULL,
                            `OrderItemId` int NOT NULL,
                            `Rating` int NOT NULL,
                            `Comment` longtext NULL,
                            `CreatedAt` datetime(6) NOT NULL,
                            `UpdatedAt` datetime(6) NULL,
                            PRIMARY KEY (`Id`),
                            KEY `IX_Reviews_ProductId` (`ProductId`),
                            KEY `IX_Reviews_UserId` (`UserId`),
                            KEY `IX_Reviews_OrderId` (`OrderId`),
                            KEY `IX_Reviews_OrderItemId` (`OrderItemId`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                    await _context.Database.ExecuteSqlRawAsync(createReviewsTable);
                    Console.WriteLine("✅ Đã tạo bảng Reviews");
                }
                else
                {
                    // Kiểm tra và thêm cột ImageUrls nếu chưa có
                    try
                    {
                        var checkColumnQuery = @"
                            SELECT COUNT(*) 
                            FROM INFORMATION_SCHEMA.COLUMNS 
                            WHERE TABLE_SCHEMA = DATABASE() 
                            AND TABLE_NAME = 'Reviews' 
                            AND COLUMN_NAME = 'ImageUrls'";
                        
                        var columnCheckCommand = connection.CreateCommand();
                        columnCheckCommand.CommandText = checkColumnQuery;
                        var columnResult = await columnCheckCommand.ExecuteScalarAsync();
                        var columnExistsResult = Convert.ToInt32(columnResult) > 0;
                        
                        if (!columnExistsResult)
                        {
                            Console.WriteLine("📦 Đang thêm cột ImageUrls vào bảng Reviews...");
                            var addColumnQuery = @"ALTER TABLE `Reviews` ADD COLUMN `ImageUrls` json NULL AFTER `Comment`";
                            await _context.Database.ExecuteSqlRawAsync(addColumnQuery);
                            Console.WriteLine("✅ Đã thêm cột ImageUrls vào bảng Reviews");
                        }
                    }
                    catch (Exception colEx)
                    {
                        Console.WriteLine($"⚠️  Lỗi kiểm tra/thêm cột ImageUrls: {colEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Lỗi kiểm tra/tạo bảng Reviews: {ex.Message}");
            }
            
            // Kiểm tra xem bảng ProductVariants cũ có cột SizeId không
            var checkOldStructure = @"
                SELECT COUNT(*) 
                FROM information_schema.columns 
                WHERE table_schema = DATABASE() 
                AND table_name = 'ProductVariants' 
                AND column_name = 'SizeId'";
            
            var hasOldStructure = false;
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = checkOldStructure;
                var result = await command.ExecuteScalarAsync();
                hasOldStructure = Convert.ToInt32(result) > 0;
            }
            catch
            {
                hasOldStructure = false;
            }

            // Luôn kiểm tra và xóa các cột cũ từ ProductVariants (kể cả khi không có dữ liệu cũ)
            if (hasOldStructure)
            {
                Console.WriteLine("🔄 Đang migrate dữ liệu từ cấu trúc cũ sang cấu trúc mới...");
                
                // Kiểm tra xem có cột Stock không (để đảm bảo có dữ liệu cũ)
                var hasStockColumn = false;
                try
                {
                    var checkStockColumn = @"
                        SELECT COUNT(*) 
                        FROM information_schema.columns 
                        WHERE table_schema = DATABASE() 
                        AND table_name = 'ProductVariants' 
                        AND column_name = 'Stock'";
                    var connection = _context.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = checkStockColumn;
                    var result = await command.ExecuteScalarAsync();
                    hasStockColumn = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    hasStockColumn = false;
                }

                if (hasStockColumn)
                {
                    // Migrate dữ liệu từ ProductVariants cũ sang ProductVariantSizes
                    var migrateData = @"
                        INSERT INTO ProductVariantSizes (ProductVariantId, SizeId, Stock)
                        SELECT Id, SizeId, Stock 
                        FROM ProductVariants 
                        WHERE SizeId IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1 FROM ProductVariantSizes pvs 
                            WHERE pvs.ProductVariantId = ProductVariants.Id 
                            AND pvs.SizeId = ProductVariants.SizeId
                        )";
                    
                    await _context.Database.ExecuteSqlRawAsync(migrateData);
                    Console.WriteLine("✅ Đã migrate dữ liệu ProductVariantSizes");
                }
                else
                {
                    Console.WriteLine("ℹ️  Không có dữ liệu cũ cần migrate");
                }
                
                // Xóa các cột cũ từ ProductVariants
                // MySQL không hỗ trợ DROP COLUMN IF EXISTS, nên cần kiểm tra trước
                try
                {
                    var dropSizeId = @"
                        ALTER TABLE ProductVariants 
                        DROP COLUMN `SizeId`";
                    await _context.Database.ExecuteSqlRawAsync(dropSizeId);
                    Console.WriteLine("✅ Đã xóa cột SizeId từ ProductVariants");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ℹ️  Không thể xóa SizeId (có thể đã không tồn tại): {ex.Message}");
                }

                try
                {
                    var dropStock = @"
                        ALTER TABLE ProductVariants 
                        DROP COLUMN `Stock`";
                    await _context.Database.ExecuteSqlRawAsync(dropStock);
                    Console.WriteLine("✅ Đã xóa cột Stock từ ProductVariants");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ℹ️  Không thể xóa Stock (có thể đã không tồn tại): {ex.Message}");
                }
                
                // Cập nhật bảng Carts nếu chưa có SizeId
                var checkCartsSizeId = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'Carts' 
                    AND column_name = 'SizeId'";
                
                var hasCartsSizeId = false;
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = checkCartsSizeId;
                    var result = await command.ExecuteScalarAsync();
                    hasCartsSizeId = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    hasCartsSizeId = false;
                }

                if (!hasCartsSizeId)
                {
                    var addSizeIdToCarts = @"
                        ALTER TABLE Carts 
                        ADD COLUMN `SizeId` int NOT NULL DEFAULT 0 AFTER `ProductVariantId`";
                    await _context.Database.ExecuteSqlRawAsync(addSizeIdToCarts);
                    Console.WriteLine("✅ Đã thêm SizeId vào Carts");
                }

                // Cập nhật bảng OrderItems nếu chưa có SizeId
                var checkOrderItemsSizeId = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'OrderItems' 
                    AND column_name = 'SizeId'";
                
                var hasOrderItemsSizeId = false;
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = checkOrderItemsSizeId;
                    var result = await command.ExecuteScalarAsync();
                    hasOrderItemsSizeId = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    hasOrderItemsSizeId = false;
                }

                if (!hasOrderItemsSizeId)
                {
                    var addSizeIdToOrderItems = @"
                        ALTER TABLE OrderItems 
                        ADD COLUMN `SizeId` int NOT NULL DEFAULT 0 AFTER `ProductVariantId`";
                    await _context.Database.ExecuteSqlRawAsync(addSizeIdToOrderItems);
                    Console.WriteLine("✅ Đã thêm SizeId vào OrderItems");
                }
                
                Console.WriteLine("✅ Đã hoàn tất migration dữ liệu");
            }

            // Kiểm tra và tạo bảng Sliders nếu chưa có
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'Sliders'";
                var result = await checkCommand.ExecuteScalarAsync();
                var slidersExists = Convert.ToInt32(result) > 0;

                if (!slidersExists)
                {
                    Console.WriteLine("📦 Đang tạo bảng Sliders...");
                    var createSlidersTable = @"
                        CREATE TABLE IF NOT EXISTS `Sliders` (
                            `Id` int NOT NULL AUTO_INCREMENT,
                            `ImageUrl` varchar(500) NOT NULL,
                            `DisplayOrder` int NOT NULL DEFAULT 0,
                            `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                            `CreatedAt` datetime(6) NOT NULL,
                            `UpdatedAt` datetime(6) NULL,
                            PRIMARY KEY (`Id`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                    await _context.Database.ExecuteSqlRawAsync(createSlidersTable);
                    Console.WriteLine("✅ Đã tạo bảng Sliders");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Lỗi kiểm tra/tạo bảng Sliders: {ex.Message}");
            }

            // Kiểm tra và tạo bảng News nếu chưa có
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'News'";
                var result = await checkCommand.ExecuteScalarAsync();
                var newsExists = Convert.ToInt32(result) > 0;

                if (!newsExists)
                {
                    Console.WriteLine("📦 Đang tạo bảng News...");
                    var createNewsTable = @"
                        CREATE TABLE IF NOT EXISTS `News` (
                            `Id` int NOT NULL AUTO_INCREMENT,
                            `Title` varchar(255) NOT NULL,
                            `Excerpt` text NOT NULL,
                            `Content` longtext NULL,
                            `ImageUrl` varchar(500) NULL,
                            `Category` varchar(100) NULL,
                            `PublishedAt` datetime(6) NOT NULL,
                            `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                            `CreatedAt` datetime(6) NOT NULL,
                            `UpdatedAt` datetime(6) NULL,
                            PRIMARY KEY (`Id`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                    await _context.Database.ExecuteSqlRawAsync(createNewsTable);
                    Console.WriteLine("✅ Đã tạo bảng News");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Lỗi kiểm tra/tạo bảng News: {ex.Message}");
            }
            
            // Luôn kiểm tra và xóa các cột cũ (kể cả khi không có cấu trúc cũ)
            // Đảm bảo ProductVariants không có SizeId và Stock
            try
            {
                // Xóa tất cả các index cũ có thể có (bao gồm cả typo)
                var indexNames = new[] { 
                    "IX_ProductVariants_ProductId_ColorId_SizeId",
                    "IX_ProducttVariants_ProductId_ColorId_SizeId" // Typo có thể có
                };
                
                foreach (var indexName in indexNames)
                {
                    try
                    {
                        var dropIndex = $@"
                            ALTER TABLE ProductVariants 
                            DROP INDEX `{indexName}`";
                        await _context.Database.ExecuteSqlRawAsync(dropIndex);
                        Console.WriteLine($"✅ Đã xóa index {indexName}");
                    }
                    catch
                    {
                        // Index có thể không tồn tại
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ℹ️  Không thể xóa index cũ: {ex.Message}");
            }

            try
            {
                var checkSizeIdColumn = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'ProductVariants' 
                    AND column_name = 'SizeId'";
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = checkSizeIdColumn;
                var result = await command.ExecuteScalarAsync();
                var hasSizeIdColumn = Convert.ToInt32(result) > 0;
                
                if (hasSizeIdColumn)
                {
                    // Xóa lại index cũ một lần nữa để chắc chắn
                    var indexNames = new[] { 
                        "IX_ProductVariants_ProductId_ColorId_SizeId",
                        "IX_ProducttVariants_ProductId_ColorId_SizeId"
                    };
                    
                    foreach (var indexName in indexNames)
                    {
                        try
                        {
                            var dropIndex = $@"
                                ALTER TABLE ProductVariants 
                                DROP INDEX `{indexName}`";
                            await _context.Database.ExecuteSqlRawAsync(dropIndex);
                        }
                        catch
                        {
                            // Index có thể không tồn tại
                        }
                    }

                    var dropSizeId = @"
                        ALTER TABLE ProductVariants 
                        DROP COLUMN `SizeId`";
                    await _context.Database.ExecuteSqlRawAsync(dropSizeId);
                    Console.WriteLine("✅ Đã xóa cột SizeId từ ProductVariants");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ℹ️  Không thể xóa SizeId: {ex.Message}");
            }

            try
            {
                var checkStockColumn = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'ProductVariants' 
                    AND column_name = 'Stock'";
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = checkStockColumn;
                var result = await command.ExecuteScalarAsync();
                var hasStockColumn = Convert.ToInt32(result) > 0;
                
                if (hasStockColumn)
                {
                    var dropStock = @"
                        ALTER TABLE ProductVariants 
                        DROP COLUMN `Stock`";
                    await _context.Database.ExecuteSqlRawAsync(dropStock);
                    Console.WriteLine("✅ Đã xóa cột Stock từ ProductVariants");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ℹ️  Không thể xóa Stock: {ex.Message}");
            }

            if (!hasOldStructure)
            {
                // Nếu không có cấu trúc cũ, vẫn cần đảm bảo các bảng khác có SizeId
                Console.WriteLine("ℹ️  Không có cấu trúc cũ cần migrate, đang kiểm tra các bảng khác...");
                
                // Đảm bảo bảng Carts có SizeId
                var checkCartsSizeId = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'Carts' 
                    AND column_name = 'SizeId'";
                
                var hasCartsSizeId = false;
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = checkCartsSizeId;
                    var result = await command.ExecuteScalarAsync();
                    hasCartsSizeId = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    hasCartsSizeId = false;
                }

                if (!hasCartsSizeId)
                {
                    try
                    {
                        // Xóa index cũ trước (nếu có)
                        try
                        {
                            var dropOldCartIndex = @"
                                ALTER TABLE Carts 
                                DROP INDEX IF EXISTS `IX_Carts_UserId_ProductVariantId`";
                            await _context.Database.ExecuteSqlRawAsync(dropOldCartIndex);
                        }
                        catch
                        {
                            // Index có thể không tồn tại
                        }

                        var addSizeIdToCarts = @"
                            ALTER TABLE Carts 
                            ADD COLUMN `SizeId` int NOT NULL DEFAULT 0 AFTER `ProductVariantId`";
                        await _context.Database.ExecuteSqlRawAsync(addSizeIdToCarts);
                        Console.WriteLine("✅ Đã thêm SizeId vào Carts");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ℹ️  Không thể thêm SizeId vào Carts: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("ℹ️  Carts đã có SizeId");
                }

                // Đảm bảo bảng OrderItems có SizeId
                var checkOrderItemsSizeId = @"
                    SELECT COUNT(*) 
                    FROM information_schema.columns 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'OrderItems' 
                    AND column_name = 'SizeId'";
                
                var hasOrderItemsSizeId = false;
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = checkOrderItemsSizeId;
                    var result = await command.ExecuteScalarAsync();
                    hasOrderItemsSizeId = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    hasOrderItemsSizeId = false;
                }

                if (!hasOrderItemsSizeId)
                {
                    try
                    {
                        var addSizeIdToOrderItems = @"
                            ALTER TABLE OrderItems 
                            ADD COLUMN `SizeId` int NOT NULL DEFAULT 0 AFTER `ProductVariantId`";
                        await _context.Database.ExecuteSqlRawAsync(addSizeIdToOrderItems);
                        Console.WriteLine("✅ Đã thêm SizeId vào OrderItems");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ℹ️  Không thể thêm SizeId vào OrderItems: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("ℹ️  OrderItems đã có SizeId");
                }

                // Đảm bảo bảng ProductVariantSizes tồn tại
                var checkProductVariantSizesExists = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'ProductVariantSizes'";
                
                var productVariantSizesExists = false;
                try
                {
                    var connection = _context.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = checkProductVariantSizesExists;
                    var result = await command.ExecuteScalarAsync();
                    productVariantSizesExists = Convert.ToInt32(result) > 0;
                }
                catch
                {
                    productVariantSizesExists = false;
                }

                if (!productVariantSizesExists)
                {
                    try
                    {
                        var createProductVariantSizesTable = @"
                            CREATE TABLE IF NOT EXISTS `ProductVariantSizes` (
                                `Id` int NOT NULL AUTO_INCREMENT,
                                `ProductVariantId` int NOT NULL,
                                `SizeId` int NOT NULL,
                                `Stock` int NOT NULL,
                                PRIMARY KEY (`Id`),
                                UNIQUE KEY `IX_ProductVariantSizes_ProductVariantId_SizeId` (`ProductVariantId`, `SizeId`),
                                KEY `IX_ProductVariantSizes_ProductVariantId` (`ProductVariantId`),
                                KEY `IX_ProductVariantSizes_SizeId` (`SizeId`),
                                CONSTRAINT `FK_ProductVariantSizes_ProductVariants_ProductVariantId` 
                                    FOREIGN KEY (`ProductVariantId`) REFERENCES `ProductVariants` (`Id`) ON DELETE CASCADE,
                                CONSTRAINT `FK_ProductVariantSizes_Sizes_SizeId` 
                                    FOREIGN KEY (`SizeId`) REFERENCES `Sizes` (`Id`) ON DELETE CASCADE
                            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                        await _context.Database.ExecuteSqlRawAsync(createProductVariantSizesTable);
                        Console.WriteLine("✅ Đã tạo bảng ProductVariantSizes");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ℹ️  Không thể tạo ProductVariantSizes: {ex.Message}");
                    }
                }

                // Đảm bảo unique index cho ProductVariants đúng
                try
                {
                    // Xóa index cũ nếu có
                    var dropOldIndex = @"
                        ALTER TABLE ProductVariants 
                        DROP INDEX IF EXISTS `IX_ProductVariants_ProductId_ColorId_SizeId`";
                    await _context.Database.ExecuteSqlRawAsync(dropOldIndex);
                    
                    // Thêm index mới nếu chưa có
                    var addNewIndex = @"
                        ALTER TABLE ProductVariants 
                        ADD UNIQUE KEY IF NOT EXISTS `IX_ProductVariants_ProductId_ColorId` (`ProductId`, `ColorId`)";
                    await _context.Database.ExecuteSqlRawAsync(addNewIndex);
                    Console.WriteLine("✅ Đã cập nhật index cho ProductVariants");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ℹ️  Không thể cập nhật index: {ex.Message}");
                }

                // Đảm bảo unique index cho Carts đúng
                try
                {
                    var dropOldCartIndex = @"
                        ALTER TABLE Carts 
                        DROP INDEX IF EXISTS `IX_Carts_UserId_ProductVariantId`";
                    await _context.Database.ExecuteSqlRawAsync(dropOldCartIndex);
                    
                    var addNewCartIndex = @"
                        ALTER TABLE Carts 
                        ADD UNIQUE KEY IF NOT EXISTS `IX_Carts_UserId_ProductVariantId_SizeId` (`UserId`, `ProductVariantId`, `SizeId`)";
                    await _context.Database.ExecuteSqlRawAsync(addNewCartIndex);
                    Console.WriteLine("✅ Đã cập nhật index cho Carts");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ℹ️  Không thể cập nhật index cho Carts: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Lỗi migration dữ liệu: {ex.Message}");
            Console.WriteLine($"   Chi tiết: {ex.InnerException?.Message ?? ex.ToString()}");
            // Không throw để không chặn việc khởi động ứng dụng
        }
    }
}

