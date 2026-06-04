using System.Net;
using System.Net.Mail;

namespace SmartKitchen.API.Services;

public class EmailService
{
    public async Task SendAsync(string name, string email, string message)
    {
        var smtp = new SmtpClient("smtp.gmail.com")
        {
            Port = 587,
            Credentials = new NetworkCredential(
                "smartkitchen840@gmail.com",
                "wgqr bari fqzs rzag" 
            ),
            EnableSsl = true
        };

        var mail = new MailMessage
        {
            From = new MailAddress("smartkitchen840@gmail.com", "Smart Kitchen Contact"),

            Subject = $" Message from {name} ({email})",

            Body = $@"
 You received a new message from your app:

 Name: {name}
 Email: {email}

 Message:
{message}

-------------------------
Reply directly to respond to the user.
",
            IsBodyHtml = false
        };

      
        mail.To.Add("smartkitchen840@gmail.com");

        
        mail.ReplyToList.Add(new MailAddress(email));

        await smtp.SendMailAsync(mail);
    }
}