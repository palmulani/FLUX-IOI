namespace Brightloop.Services
{
    public interface IAiService
    {
        Task<string> AskCopilotAsync(string userPrompt, string contextJson);
        Task<string> AnalyzeChurnRiskAsync(string customerName, int activeSubs, decimal totalPaid, int overdueCount);
        Task<string> RecommendUpsellAsync(string customerName, string currentPlan, decimal mrr);
    }
}
