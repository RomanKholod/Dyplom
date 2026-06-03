namespace RentalManagement.Domain.Enums;

public enum PropertyType
{
    Residential = 1,
    Commercial = 2,
    Industrial = 3,
    Land = 4
}

public enum PropertyStatus
{
    Available = 1,
    Rented = 2,
    UnderMaintenance = 3,
    Reserved = 4
}

public enum UnitStatus
{
    Available = 1,
    Occupied = 2,
    UnderRepair = 3,
    Reserved = 4
}

public enum ContractStatus
{
    Draft = 1,
    Active = 2,
    Expired = 3,
    Terminated = 4,
    Suspended = 5
}

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3,
    PartiallyPaid = 4,
    Cancelled = 5
}

public enum InvoiceType
{
    Rent = 1,
    Utility = 2,
    Maintenance = 3,
    Deposit = 4,
    Fine = 5
}

public enum ContactType
{
    Phone = 1,
    Email = 2,
    Telegram = 3,
    Viber = 4
}

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Accountant = 3,
    Viewer = 4
}
