public record LoginRequest
{
    public string userName { get; set; }
    public string nPwd { get; set; }
    public string pPwd { get; set; } = string.Empty;
}
