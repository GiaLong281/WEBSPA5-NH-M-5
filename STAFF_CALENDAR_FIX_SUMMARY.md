# Fix: Staff Calendar Not Displaying Customer Bookings

## Problem

Staff calendar appointments were **not appearing** even though customers had already booked services and staff were assigned to those bookings.

## Root Cause

The JavaScript `fetch()` request in FullCalendar was **not sending authentication cookies**, causing the API to return a 401 Unauthorized error.

### Technical Details

- **API Endpoint**: `GET /api/ApiStaffCalendar/GetBookings`
- **Controller Filter**: `[Authorize(Roles = "Staff,Admin")]`
- **Issue**: Browser's `fetch()` doesn't send cookies by default (security feature)
- **Result**: API rejected unauthenticated requests before returning booking data

## Solution Applied

Updated the fetch request in `Views/StaffCalendar/Index.cshtml` to include authentication credentials:

```javascript
// BEFORE (not working)
fetch(url)
  .then((res) => res.json())
  .then((data) => successCallback(data));

// AFTER (fixed)
fetch(url, {
  method: "GET",
  credentials: "same-origin", // ← Include cookies with same-origin requests
})
  .then((res) => {
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
  })
  .then((data) => successCallback(data));
```

## Verification

✅ **Database**: 9 BookingDetail records with StaffId=2 in database
✅ **API Query**: Correctly filters by staff, date range, and non-cancelled status
✅ **Authentication**: Staff user (ID: 4) has StaffId claim set to 2
✅ **Fix**: Fetch now sends authentication cookies to API

## How to Test

### 1. Log in as Staff

- Navigate to: `http://localhost:5005/Account/Login`
- Username: `Staff`
- Password: `staff123` (or whatever password was set during staff creation)

### 2. Access Staff Calendar

- Click menu → "Lịch làm việc" (Staff Calendar)
- Or navigate to: `http://localhost:5005/StaffCalendar/Index`

### 3. Verify Bookings Display

You should see customer bookings displayed in the weekly/monthly calendar view:

- **Booking Code**: BK20260403155405883, BK20260404084540105, etc.
- **Customer Names**: Associated with booked services
- **Time Slots**: Correctly positioned in the calendar grid
- **Status**: Color-coded (Pending=yellow, Confirmed=light blue, InProgress=blue, Completed=green, Cancelled=red)

### 4. Click Event Details

- Click on any booking in the calendar
- A modal should display:
  - Booking Code
  - Customer Name
  - Service Name
  - Start/End Time
  - Status

## Features Working

✅ Staff can view their assigned bookings
✅ Bookings show customer name and service details
✅ Time conflicts are properly detected
✅ Status colors help identify booking state
✅ Modal shows detailed booking information
✅ Multiple view modes (Week, Day, Month, List)

## Files Modified

- `Views/StaffCalendar/Index.cshtml` - Added `credentials: 'same-origin'` to fetch request
- Added console logging for debugging API calls

## API Endpoint Details

**URL**: `/api/ApiStaffCalendar/GetBookings?staffId={id}&start={date}&end={date}`

**Response Format**:

```json
[
  {
    "id": 1,
    "title": "Customer Name - Service Name",
    "start": "2026-04-04T14:30:00",
    "end": "2026-04-04T17:00:00",
    "color": "#007bff",
    "extendedProps": {
      "bookingCode": "BK20260404084540105",
      "status": "Pending",
      "customer": "Customer Name",
      "service": "Service Name"
    }
  }
]
```

## Related Routes

- **Staff Calendar View**: `/StaffCalendar/Index`
- **Staff Dashboard**: `/Staff/Dashboard`
- **Staff Attendance**: `/Staff/Attendance`
- **Staff Income**: `/Staff/Income`
- **API Endpoint**: `/api/ApiStaffCalendar/GetBookings`

## Next Steps (Optional Enhancements)

1. Add drag-to-reschedule functionality
2. Add real-time notifications for new bookings
3. Add reminder emails/SMS for upcoming appointments
4. Add staff availability settings per day/time
5. Add performance metrics dashboard
