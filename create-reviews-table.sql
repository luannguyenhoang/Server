-- Script để tạo bảng Reviews
-- Chạy script này trên database của bạn

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

