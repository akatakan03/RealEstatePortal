namespace RealEstatePortal.Infrastructure.Email;

public class EmailSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public string FromAddress { get; set; } = "noreply@realestate.local";
}