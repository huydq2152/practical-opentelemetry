using System.Diagnostics;
using Grpc.Core;
using OpenTelemetry;
using RiskEvaluator.Services.Rules;

namespace RiskEvaluator.Services;

public class EvaluatorService : Evaluator.EvaluatorBase
{
    private readonly ILogger<EvaluatorService> _logger;
    private readonly IEnumerable<IRule> _rules;

    public EvaluatorService(ILogger<EvaluatorService> logger, IEnumerable<IRule> rules)
    {
        _logger = logger;
        _rules = rules;
    }

    public override Task<RiskEvaluationReply> Evaluate(RiskEvaluationRequest request, ServerCallContext context)
    {
        var clientId = Baggage.GetBaggage("client.Id");
        _logger.LogInformation("Evaluating risk for {Email} {id}", request.Email, clientId);

        // Activity.Current?.SetTag("client.Id", clientId); // No need this line, BaggageProcessor will do it for us

        var score = _rules.Sum(rule => rule.Evaluate(request));

        var level = score switch
        {
            <= 5 => RiskLevel.Low,
            <= 20 => RiskLevel.Medium,
            _ => RiskLevel.High
        };

        _logger.LogInformation("Risk level for {Email} is {Level}", request.Email, level);

        Activity.Current?.SetTag("evaluation.email", request.Email);

        Activity.Current?.AddEvent(new ActivityEvent(
            "RiskResult",
            default,
            new ActivityTagsCollection(new KeyValuePair<string, object?>[]
            {
                new("risk.score", score),
                new("risk.level", level)
            })));

        return Task.FromResult(new RiskEvaluationReply()
        {
            RiskLevel = level,
        });
    }
}