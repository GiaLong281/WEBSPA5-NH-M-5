namespace SpaN5.Models
{
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Accepted,
        InProgress,
        Completed,
        Cancelled
    }

    public enum PaymentMethod
    {
        Cash,
        Momo,
        VNPay,
        BankTransfer
    }

    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed
    }

    public enum DetailStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }
}