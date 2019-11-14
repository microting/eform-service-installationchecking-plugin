namespace ServiceInstallationCheckingPlugin.Messages
{
    public class eFormCompleted
    {
        public int microtingUId { get; protected set; }
        public int checkUId { get; protected set; }

        public eFormCompleted(int microtingUId, int checkUId)
        {
            this.microtingUId = microtingUId;
            this.checkUId = checkUId;
        }
    }
}