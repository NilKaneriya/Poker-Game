namespace Poker.Network
{
    /// <summary>Why an HTTP call failed. Status 0 means the server could not be reached.</summary>
    public class ApiFailure
    {
        public long Status;
        public string Message;
        public bool IsNetworkError => Status == 0;
    }
}
