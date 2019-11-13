namespace ServiceInstallationCheckingPlugin.Messages
{
    public class eFormCompleted
    {
        public int caseId { get; protected set; }
        public int microtingUId { get; protected set; }

        public eFormCompleted(int caseId, int microtingUId)
        {
            this.caseId = caseId;
            this.microtingUId = microtingUId;
        }
    }
}