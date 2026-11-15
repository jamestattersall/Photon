namespace Photon.Models
{
    public class LoginResponse
    {
       public string? Message { get; set; }
       public bool IsAuthenticated { get; set; }
       public UserStarter? UserStarter { get; set; }
    }
}
