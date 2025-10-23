namespace TestJwt.Model
{
    public class LoginResponse
    {
       public string Message { get; set; }
       public string? Token { get; set; }
       public UserStarter? UserStarter { get; set; }
    }
}
