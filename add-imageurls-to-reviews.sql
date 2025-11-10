-- Script để thêm cột ImageUrls vào bảng Reviews
-- Chạy script này nếu bảng Reviews đã tồn tại

-- Kiểm tra và thêm cột ImageUrls nếu chưa có
ALTER TABLE `Reviews` 
ADD COLUMN IF NOT EXISTS `ImageUrls` json NULL AFTER `Comment`;

