namespace Myrtille.Common.Interfaces
{
    public interface IAuthenticationPlugin
    {
        bool CanProcess(string request);

        string Domain { get; set; }

        string UserName { get; set; }

        string Password { get; set; }
    }
}