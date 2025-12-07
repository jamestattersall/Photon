namespace Photon.Models
{
    public class LoginResponse
    {
       public string? Message { get; set; }
       public string? Token { get; set; }
       public UserStarter? UserStarter { get; set; }
       public string? Username{ get; set; }
    }
}
