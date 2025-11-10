-- Script để tạo bảng PasswordResetTokens
-- Chạy script này trên database của bạn để thêm chức năng quên mật khẩu

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

