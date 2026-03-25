namespace SpaN5.Models
{
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        InProgress,
        Completed,
        Cancelled
    }

    public enum PaymentMethod
    {
        Cash,
        Momo,
        VNPay
    }

    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed
    }
}