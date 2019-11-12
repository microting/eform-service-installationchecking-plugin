namespace ServiceInstallationCheckingPlugin.Messages
{
    public class eFormCompleted
    {
        public int caseId { get; protected set; }
        public int checkListId { get; protected set; }

        public eFormCompleted(int caseId, int checkListId)
        {
            this.caseId = caseId;
            this.checkListId = checkListId;
        }
    }
}