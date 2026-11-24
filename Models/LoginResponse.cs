namespace Photon.Models
{
    public class LoginResponse
    {
       public string? Message { get; set; }
       public bool IsAuthenticated { get; set; }
       public UserStarter? UserStarter { get; set; }
    }

    public class InitialResponse
    {
        public string userName { get; set; } = string.Empty;
        public bool requireNetworkLogin { get; set; } 
    }
}
