namespace LibraryAdminApp
{
    // คลาสนี้ต้องมีชื่อ Property ตรงกับ key ใน JSON ที่ Google Apps Script ส่งกลับมา
    // (BookingID, Timestamp, StudentID, Name, RoomName, BookingDate, Status)
    public class Booking
    {
        public string BookingID { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string StudentID { get; set; } = "";
        public string Name { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string BookingDate { get; set; } = "";
        public string Status { get; set; } = "";
    }

    // โครงสร้างของ response ที่ Apps Script ส่งกลับมาตอน GET
    public class ApiListResponse
    {
        public string Status { get; set; } = "";
        public List<Booking> Data { get; set; } = new();
    }

    //(สร้าง/อัปเดต)
    public class ApiActionResponse
    {
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
    }
}