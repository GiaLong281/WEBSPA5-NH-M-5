# 🔍 Staff Calendar Debugging Guide

## Issue

Staff calendar is not displaying customer bookings even though:

- ✅ Customers have booked services
- ✅ Staff are assigned to bookings
- ✅ Bookings exist in the database

## Quick Verification

### Step 1: Check Authentication

Open your browser and navigate to:

```
http://localhost:5005/api/ApiStaffCalendar/debug/whoami
```

**Expected Response**:

```json
{
  "username": "Staff",
  "role": "Staff",
  "staffId": 2,
  "isAuthenticated": true,
  "claims": [...]
}
```

**If you see**:

- `"staffId": null` → The StaffId claim not being set during login
- `"isAuthenticated": false` → Not logged in or session expired
- `"staffId": 0` → StaffId is 0, should be 2

### Step 2: Check Browser Console for Errors

1. Open Developer Tools in your browser (Press **F12**)
2. Go to **Console** tab
3. Look for any error messages or the debug logs like:
   - `Initial Staff ID from server: 2`
   - `Using staffId: 2`
   - `Fetching bookings from: /api/ApiStaffCalendar/GetBookings?staffId=2&...`
   - `Response status: 200`
   - `Bookings loaded: 7 events`

### Step 3: Check Network Requests

1. In Developer Tools, go to **Network** tab
2. Reload the page
3. Look for requests to `/api/ApiStaffCalendar/GetBookings`
4. Click on that request
5. Check the **Response** tab to see the data returned

**Expected Response**:

```json
[
  {
    "id": 1,
    "title": "Customer Name - Service Name",
    "start": "2026-04-04T14:30:00",
    "end": "2026-04-04T17:00:00",
    "color": "#ffc107",
    "extendedProps": {...}
  },
  ...
]
```

## Common Issues & Solutions

### 1. **staffId is null**

**Symptom**: `"staffId": null` in `/api/ApiStaffCalendar/debug/whoami`

**Cause**: The StaffId claim wasn't set during login

**Solution**:

- Log out (click Logout button)
- Clear browser cookies (F12 → Application → Cookies, delete all)
- Log back in
- Try again

### 2. **API returns 401 Unauthorized**

**Symptom**: Network tab shows `401` response for GetBookings request

**Cause**: Browser not sending authentication cookies

**Solution**:

- **Hard refresh the page**: `Ctrl+F5` (Windows) or `Cmd+Shift+R` (Mac)
- Clear browser cache
- Try a different browser
- Check if cookies are enabled in browser settings

### 3. **API returns 200 but no bookings array**

**Symptom**: Response shows `[]` (empty array)

**Cause**: Either no bookings exist for that date range, or the query parameters are wrong

**Solution**:

- Check what date range the calendar is showing
- Try adjusting to April 2026 dates (bookings are in 04/2026)
- Open browser console and look at the URL being requested

### 4. **Calendar displays but events don't appear**

**Symptom**: Calendar loads, but time slots are empty

**Cause**: Data is loading but FullCalendar not rendering properly

**Solution**:

- Clear browser cache (`Ctrl+Shift+Delete`)
- Hard refresh (`Ctrl+F5`)
- Check browser console for JavaScript errors
- Verify the response format is correct (check Network tab)

### 5. **"No staff ID available" error in console**

**Symptom**: Console shows `No staff ID available` or `Using staffId: null`

**Cause**: The initialStaffId wasn't passed from server to frontend

**Solution**:

1. Check `/api/ApiStaffCalendar/debug/whoami` response
2. If `staffId` is null there too, the issue is on the server
3. If `staffId` is present on server but null in JavaScript, it's a view rendering issue

## Step-by-Step Testing Process

1. **Clear everything and start fresh**:

   ```
   Press Ctrl+Shift+Delete → Clear all cookies/cache
   Close and reopen browser
   Navigate to http://localhost:5005/Account/Login
   ```

2. **Log in as Staff**:

   ```
   Username: Staff
   Password: [staff password]
   ```

3. **Test authentication endpoint**:

   ```
   Navigate to: http://localhost:5005/api/ApiStaffCalendar/debug/whoami
   Check that staffId = 2
   ```

4. **Go to calendar**:

   ```
   Click "Lịch làm việc" from menu
   Or navigate to: http://localhost:5005/StaffCalendar/Index
   ```

5. **Check browser console** (F12):

   ```
   Look for: "Initial Staff ID from server: 2"
   Look for: "Fetching bookings from: /api/..."
   Look for: "Bookings loaded: X events"
   ```

6. **If still empty**, check Network tab:
   ```
   Look for /api/ApiStaffCalendar/GetBookings request
   Check Status code (should be 200)
   Check Response (should contain booking objects)
   ```

## Advanced Debugging

### Check if Browser Sent Cookies

1. Open Developer Tools (F12)
2. Go to Network tab
3. Make a request (reload page or navigate)
4. Click on `/api/ApiStaffCalendar/GetBookings` request
5. Click **Request Headers**
6. Look for: `Cookie: .AspNetCore.Cookies=...`

**If no Cookie header**: This is the problem! Cookies not being sent.

### Force Cookies in Fetch

Edit `Views/StaffCalendar/Index.cshtml` and make sure this line exists:

```javascript
fetch(url, {
  method: "GET",
  credentials: "same-origin", // ← This is crucial!
});
```

### Check Server-Side Logs

If you have access to the application logs, look for:

- Authentication failures
- Authorization denials
- Database query errors
- Null reference exceptions

## Database Verification

If everything passes the above tests but still no bookings show:

1. Verify bookings exist for the current staff:

```sql
SELECT * FROM BookingDetails WHERE StaffId = 2;
```

2. Should return 7-9 rows

3. If empty, bookings need to be created/assigned through the UI

## Booking Creation Checklist

To create a test booking:

1. Log out and log in as **Customer**
2. Go to "Đặt lịch" (Book Service)
3. Fill in booking form:
   - Service: Choose one
   - Date/Time: April 4-6, 2026
   - Staff: Select "Staff" (Trần bình thanh)
   - Branch: Đông Nai
4. Submit booking
5. Log out and log back in as **Staff**
6. Go to calendar - should see new booking

## Need More Help?

Check these files for potential issues:

- `Views/StaffCalendar/Index.cshtml` - Frontend JavaScript
- `Controllers/ApiStaffCalendarController.cs` - Backend API logic
- `Controllers/StaffCalendarController.cs` - View rendering logic
- `Program.cs` - Authentication setup

## Success Indicator

✅ **Calendar working when you see**:

- Browser console shows `"Bookings loaded: X events"`
- Calendar time slots display colored blocks with customer names
- Clicking an event shows booking details in a modal
- Only your assigned bookings appear (Staff privacy)
