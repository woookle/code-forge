namespace CodeForgeAPI.Models;

public class EmailSettings
{
    public string MailServer { get; set; } = "smtp.yandex.ru";
    public int MailPort { get; set; } = 587;
    public string SenderName { get; set; } = "CodeGenerator";
    public string SenderEmail { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;
    public bool UseStartTls { get; set; } = false;
}
