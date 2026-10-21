-- =============================================================================
-- MySQL Script สำหรับรันใน phpMyAdmin (WAMP Server)
-- ฐานข้อมูล: library_reservation
-- ตารางหลัก: users, rooms, reservations, penalties, maintenance_reports, notification_logs
-- รหัสห้อง: ห้องใหญ่ = 601 (ขั้นต่ำ 7 คน), ห้องกลาง = 602, 603, 604 (ขั้นต่ำ 5 คน)
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `library_reservation`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `library_reservation`;

-- 1. ตารางผู้ใช้งาน (users) สำหรับระบบ Login จริง
CREATE TABLE IF NOT EXISTS `users` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `username` VARCHAR(64) NOT NULL UNIQUE COMMENT 'รหัสนักศึกษา หรือ รหัสบุคลากร ใช้สำหรับ Login',
    `password_hash` VARCHAR(255) NOT NULL COMMENT 'รหัสผ่านเข้าสู่ระบบ',
    `full_name` VARCHAR(150) NOT NULL,
    `email` VARCHAR(150) NOT NULL,
    `role` VARCHAR(32) NOT NULL DEFAULT 'Student' COMMENT 'Student, Staff, Admin',
    `daily_quota_minutes` INT NOT NULL DEFAULT 180 COMMENT 'โควตาการจองต่อวัน (180 นาที = 3 ชั่วโมง)',
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. ตารางห้องบริการ (rooms) มีเฉพาะ 4 ห้อง: ห้องใหญ่ 601 (ขั้นต่ำ 7 คน), ห้องกลาง 602, 603, 604 (ขั้นต่ำ 5 คน)
CREATE TABLE IF NOT EXISTS `rooms` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `room_code` VARCHAR(32) NOT NULL UNIQUE COMMENT '601 (ห้องใหญ่), 602, 603, 604 (ห้องกลาง)',
    `room_name` VARCHAR(150) NOT NULL,
    `room_type` VARCHAR(32) NOT NULL COMMENT 'Medium, Large',
    `floor` INT NOT NULL DEFAULT 6,
    `building` VARCHAR(150) NOT NULL DEFAULT 'อาคารหอสมุดกลาง',
    `min_capacity` INT NOT NULL COMMENT 'ขั้นต่ำ 5 คน (Medium: 602,603,604) หรือ 7 คน (Large: 601)',
    `max_capacity` INT NOT NULL,
    `max_booking_hours` INT NOT NULL DEFAULT 3,
    `status` VARCHAR(32) NOT NULL DEFAULT 'Available',
    `qr_secret_key` VARCHAR(128) NOT NULL,
    `equipments_json` TEXT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. ตารางการจองห้อง (reservations) เก็บจำนวนผู้เข้าใช้งานเป็นตัวเลข (expected_attendees)
CREATE TABLE IF NOT EXISTS `reservations` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `booking_no` VARCHAR(32) NOT NULL UNIQUE,
    `room_id` CHAR(36) NOT NULL,
    `host_username` VARCHAR(64) NOT NULL COMMENT 'รหัสผู้จองหลัก',
    `host_full_name` VARCHAR(150) NOT NULL,
    `purpose` VARCHAR(500) NOT NULL,
    `start_time` DATETIME NOT NULL,
    `end_time` DATETIME NOT NULL,
    `expected_attendees` INT NOT NULL COMMENT 'จำนวนผู้เข้าใช้งาน (ตัวเลข)',
    `status` VARCHAR(32) NOT NULL DEFAULT 'Reserved' COMMENT 'Reserved, CheckedIn, Completed, AutoReleased, Cancelled, EmergencyOverride',
    `checkin_pin_code` VARCHAR(16) NOT NULL,
    `cancel_reason` VARCHAR(500) NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `checked_in_at` DATETIME NULL,
    `pre_start_notified` TINYINT(1) NOT NULL DEFAULT 0,
    `pre_end_notified` TINYINT(1) NOT NULL DEFAULT 0,
    INDEX `idx_reservations_room_time` (`room_id`, `start_time`, `end_time`),
    INDEX `idx_reservations_status_start` (`status`, `start_time`),
    CONSTRAINT `fk_reservations_room` FOREIGN KEY (`room_id`) REFERENCES `rooms`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. ตารางประวัติการระงับสิทธิ์ 2 วัน (penalties)
CREATE TABLE IF NOT EXISTS `penalties` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `username` VARCHAR(64) NOT NULL,
    `full_name` VARCHAR(150) NOT NULL,
    `booking_id` CHAR(36) NOT NULL,
    `booking_no` VARCHAR(32) NOT NULL,
    `room_name` VARCHAR(150) NOT NULL,
    `reason` VARCHAR(500) NOT NULL,
    `penalty_days` INT NOT NULL DEFAULT 2,
    `ban_start_at` DATETIME NOT NULL,
    `ban_end_at` DATETIME NOT NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    INDEX `idx_penalties_username` (`username`, `is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. ตารางแจ้งซ่อมอุปกรณ์ชำรุด (maintenance_reports)
CREATE TABLE IF NOT EXISTS `maintenance_reports` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `room_id` CHAR(36) NOT NULL,
    `room_code` VARCHAR(32) NOT NULL,
    `room_name` VARCHAR(150) NOT NULL,
    `equipment_name` VARCHAR(150) NOT NULL,
    `reported_by` VARCHAR(150) NOT NULL,
    `title` VARCHAR(255) NOT NULL,
    `description` TEXT NOT NULL,
    `priority` VARCHAR(32) NOT NULL DEFAULT 'Medium',
    `status` VARCHAR(32) NOT NULL DEFAULT 'Open',
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `resolved_at` DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. ตารางบันทึกการส่งแจ้งเตือน (notification_logs)
CREATE TABLE IF NOT EXISTS `notification_logs` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `channel` VARCHAR(64) NOT NULL,
    `recipient` VARCHAR(150) NOT NULL,
    `subject` VARCHAR(255) NOT NULL,
    `message` TEXT NOT NULL,
    `sent_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- ข้อมูลเริ่มต้น (Seed Data): ห้องบริการ 4 ห้อง (601, 602, 603, 604) และบัญชีผู้ใช้เริ่มต้น
-- =============================================================================

INSERT IGNORE INTO `users` (`id`, `username`, `password_hash`, `full_name`, `email`, `role`, `daily_quota_minutes`) VALUES
('11111111-1111-1111-1111-111111111101', '6610400001', '123456', 'กิตติพงศ์ รัตนโชติ', 'kittipong.r@ku.th', 'Student', 180),
('11111111-1111-1111-1111-111111111102', '6610400002', '123456', 'พิมพ์ชนก วงศ์สวัสดิ์', 'pimchanok.w@ku.th', 'Student', 180),
('11111111-1111-1111-1111-111111111103', 'STAFF-001', '123456', 'ดร.ปิยะพงษ์ วิชาการ', 'piyapong.w@ku.th', 'Staff', 180),
('11111111-1111-1111-1111-111111111104', 'admin', 'admin123', 'เจ้าหน้าที่ดูแลระบบห้องสมุด', 'admin.library@ku.th', 'Admin', 480);

DELETE FROM `rooms` WHERE `room_code` NOT IN ('601', '602', '603', '604');

INSERT INTO `rooms` (`id`, `room_code`, `room_name`, `room_type`, `floor`, `building`, `min_capacity`, `max_capacity`, `max_booking_hours`, `status`, `qr_secret_key`, `equipments_json`) VALUES
('22222222-2222-2222-2222-222222222201', '601', 'ห้อง 601 (ห้องขนาดใหญ่ / Large Seminar Room)', 'Large', 6, 'อาคารหอสมุดกลาง', 7, 15, 3, 'Available', 'secret-room-601', '[{"Code":"SMART_TV","Name":"Interactive Smart Board 75 นิ้ว","Quantity":1},{"Code":"PROJECTOR","Name":"เลเซอร์โปรเจคเตอร์ 4K","Quantity":1},{"Code":"WHITEBOARD","Name":"ไวท์บอร์ดขนาดใหญ่","Quantity":2},{"Code":"POWER_PLUG","Name":"ปลั๊กไฟรอบโต๊ะประชุมใหญ่","Quantity":15}]'),
('22222222-2222-2222-2222-222222222101', '602', 'ห้อง 602 (ห้องขนาดกลาง 1 / Medium Study Room 1)', 'Medium', 6, 'อาคารหอสมุดกลาง (ใกล้ห้อง Admin)', 5, 8, 3, 'Available', 'secret-room-602', '[{"Code":"SMART_TV","Name":"Smart TV 55 นิ้ว","Quantity":1},{"Code":"WHITEBOARD","Name":"กระดานไวท์บอร์ด","Quantity":1},{"Code":"POWER_PLUG","Name":"ปลั๊กไฟกลางโต๊ะ","Quantity":8}]'),
('22222222-2222-2222-2222-222222222102', '603', 'ห้อง 603 (ห้องขนาดกลาง 2 / Medium Study Room 2)', 'Medium', 6, 'อาคารหอสมุดกลาง (ใกล้ห้อง Admin)', 5, 8, 3, 'Available', 'secret-room-603', '[{"Code":"SMART_TV","Name":"Smart TV 55 นิ้ว","Quantity":1},{"Code":"WHITEBOARD","Name":"กระดานไวท์บอร์ด","Quantity":1},{"Code":"POWER_PLUG","Name":"ปลั๊กไฟกลางโต๊ะ","Quantity":8}]'),
('22222222-2222-2222-2222-222222222103', '604', 'ห้อง 604 (ห้องขนาดกลาง 3 / Medium Study Room 3)', 'Medium', 6, 'อาคารหอสมุดกลาง (ใกล้ห้อง Admin)', 5, 10, 3, 'Available', 'secret-room-604', '[{"Code":"SMART_TV","Name":"Smart TV 65 นิ้ว (Wireless Share)","Quantity":1},{"Code":"WHITEBOARD","Name":"กระดานไวท์บอร์ดกระจก","Quantity":2},{"Code":"POWER_PLUG","Name":"ปลั๊กไฟและพอร์ต USB-C","Quantity":10}]')
ON DUPLICATE KEY UPDATE
    `room_code` = VALUES(`room_code`),
    `room_name` = VALUES(`room_name`),
    `room_type` = VALUES(`room_type`),
    `floor` = VALUES(`floor`),
    `min_capacity` = VALUES(`min_capacity`),
    `max_capacity` = VALUES(`max_capacity`),
    `equipments_json` = VALUES(`equipments_json`);

