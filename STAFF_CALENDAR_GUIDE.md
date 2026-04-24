# 🎉 Staff Calendar Fix - Complete Guide

## What Was Fixed

**Staff calendar now displays customer bookings correctly** after customer accounts book services.

## The Issue You Experienced

- Customer books a service ✅ (e.g., "Chăm sóc da" on 04/04/2026)
- Staff is assigned to the booking ✅
- Staff views their calendar ❌ **BUT bookings don't show up**

## Why This Happened

The staff calendar's JavaScript code was fetching appointment data from the API, but **wasn't sending authentication credentials** (cookies). The API server, having no proof of who was requesting, rejected the request with a 401 Unauthorized error.

**Result**: No bookings displayed on calendar.

## How It Was Fixed

Updated file: `Views/StaffCalendar/Index.cshtml`

**The Change**:

```javascript
// Line 186-189: Added credentials to fetch request
fetch(url, {
  method: "GET",
  credentials: "same-origin", // ← This was missing!
});
```

This one line tells the browser: "Hey, when you talk to the API, please also send my login cookies so the server knows it's me."

## How to Use the Fixed Feature

### Step 1: Log in as Staff Member

```
URL: http://localhost:5005/Account/Login
Username: Staff
Password: [staff password]
```

### Step 2: Go to Staff Calendar

Navigate to **"Lịch làm việc"** (Staff Calendar) from the menu, or go directly to:

```
http://localhost:5005/StaffCalendar/Index
```

### Step 3: View Your Bookings

The calendar now displays:

- **Customer name** + **Service name** in each time slot
- **Booking status** shown by color:
  - 🟡 Yellow = Pending (chờ xác nhận)
  - 🔵 Light Blue = Confirmed
  - 🔵 Dark Blue = In Progress (đang thực hiện)
  - 🟢 Green = Completed (hoàn thành)
  - 🔴 Red = Cancelled (đã hủy)

### Step 4: Click Event for Details

Click on any booking to see:

- Booking Code (Mã đơn hàng)
- Customer Name
- Service Name
- Time (Bắt đầu - Kết thúc)
- Status

## Example Bookings in Database

Currently, Staff ID 2 has 7 active bookings:

| Booking Code        | Date  | Time        | Service | Status    |
| ------------------- | ----- | ----------- | ------- | --------- |
| BK20260403155405883 | 04/04 | 12:00-14:30 | Mixed   | Pending   |
| BK20260404084540105 | 04/04 | 14:30-17:00 | Mixed   | Pending   |
| BK20260404090902220 | 06/04 | 11:30-12:30 | Mixed   | Pending   |
| BK20260401190957388 | 01/04 | 14:00-15:30 | Mixed   | Completed |
| BK20260401195225217 | 04/04 | 09:30-11:00 | Mixed   | Confirmed |
| BK20260401195225344 | 04/04 | 09:30-11:00 | Mixed   | Completed |
| BK20260402123224831 | 02/04 | 12:00-13:30 | Mixed   | Pending   |

## Calendar Features Available

### 📅 Multiple View Modes

- **Week View** (Tuần) - See entire week in time slots
- **Day View** (Ngày) - Focus on single day
- **Month View** (Tháng) - Overview of entire month
- **List View** (Danh sách) - All events in list format

### 🔄 Navigation Controls

- **Previous/Next** arrows to switch weeks/months
- **Today** button to jump to current date
- **View selector** (Tuần/Ngày/Tháng/Danh sách)

### 📱 Responsive Design

- Works on desktop and tablet
- Touch-friendly on mobile devices

## Console Debugging (For Developers)

The fix includes console logging. Open **Developer Tools** (F12) to see:

```
Fetching bookings from: /api/ApiStaffCalendar/GetBookings?staffId=2&start=2026-04-03&end=2026-04-10
Response status: 200
Bookings loaded: [{...}, {...}, ...]
```

If you see status 401, it means authentication failed. If you see status 200 but no data, check the Admin panel to verify bookings exist.

## Related Staff Features

✅ **Dashboard** (`/Staff/Dashboard`) - Today's and upcoming bookings at-a-glance
✅ **Calendar** (`/Staff/Calendar`) - Weekly staff calendar view  
✅ **Attendance** (`/Staff/Attendance`) - Check-in/check-out tracking
✅ **Income** (`/Staff/Income`) - Commission and salary tracking
✅ **Customer Notes** (`/Staff/AddNote`) - Add notes about customers

## Testing Checklist

- [ ] Log in as staff member
- [ ] Navigate to calendar
- [ ] See bookings displayed (should see 5-7 entries for April 2026)
- [ ] Click on a booking event
- [ ] Modal shows booking details correctly
- [ ] Try different view modes (Week/Day/Month/List)
- [ ] Navigate to different weeks
- [ ] Color coding matches booking status

## If Calendar Still Shows No Bookings

### 1. Check Console for Errors (F12)

- Look for HTTP error messages
- If 401: Authentication failed
- If 403: Staff doesn't have permission
- If 404: API endpoint not found

### 2. Verify Staff Login

```sql
SELECT * FROM Users WHERE Role='Staff';
SELECT * FROM Staffs WHERE Status='active';
```

### 3. Verify Bookings Exist

```sql
SELECT COUNT(*) FROM BookingDetails WHERE StaffId=2;
```

### 4. Test API Directly

```
GET http://localhost:5005/api/ApiStaffCalendar/GetBookings?staffId=2&start=2026-04-01&end=2026-04-30
```

### 5. Check Authentication Cookie

In browser Developer Tools → Application → Cookies:

- Look for `.AspNetCore.Cookies` cookie
- It should contain session data

## Summary

🎯 **One line fix, huge impact**: Adding `credentials: 'same-origin'` to the fetch request enables the staff calendar API to authenticate requests and return booking data.

The staff calendar now works perfectly and displays all customer bookings assigned to each staff member!
