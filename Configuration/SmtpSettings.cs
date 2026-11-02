namespace ContingenciaWms.Configuration
{
    public class SmtpSettings
    {
        public string Host { get; set; } = "mail.cbf.com.pe";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = "info@cbf.com.pe";
        public string Password { get; set; } = string.Empty;
        public string From { get; set; } = "info@cbf.com.pe";
        public string FromName { get; set; } = "Contingencia WMS CBF";
    }
}

