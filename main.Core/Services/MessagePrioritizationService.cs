using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Message Prioritization Service for routing messages based on priority and rules
/// </summary>
public interface IMessagePrioritizationService
{
    /// <summary>
    /// Calculate priority for a message
    /// </summary>
    Task<int> CalculatePriorityAsync(
        Dictionary<string, string> record,
        Dictionary<string, object> messageProperties,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Route message to appropriate topic/queue based on priority and rules
    /// </summary>
    Task<string> RouteMessageAsync(
        string interfaceName,
        Dictionary<string, string> record,
        Dictionary<string, object> messageProperties,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add routing rule
    /// </summary>
    Task AddRoutingRuleAsync(RoutingRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get routing rules for an interface
    /// </summary>
    Task<List<RoutingRule>> GetRoutingRulesAsync(string interfaceName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Routing rule
/// </summary>
public class RoutingRule
{
    public string InterfaceName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 0; // Higher number = higher priority
    public string? TopicName { get; set; } // Override topic name
    public Func<Dictionary<string, string>, Dictionary<string, object>, bool> Condition { get; set; } = (_, _) => true;
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

/// <summary>
/// Message Prioritization Service implementation
/// </summary>
public class MessagePrioritizationService : IMessagePrioritizationService
{
    private readonly ILogger<MessagePrioritizationService>? _logger;
    private readonly Dictionary<string, List<RoutingRule>> _routingRules = new();
    private readonly object _lock = new object();

    public MessagePrioritizationService(ILogger<MessagePrioritizationService>? logger = null)
    {
        _logger = logger;
    }

    public Task<int> CalculatePriorityAsync(
        Dictionary<string, string> record,
        Dictionary<string, object> messageProperties,
        CancellationToken cancellationToken = default)
    {
        // Default priority
        var priority = 0;

        // Check routing rules for this interface
        var interfaceName = messageProperties.TryGetValue("InterfaceName", out var ifName) 
            ? ifName.ToString() ?? string.Empty 
            : string.Empty;

        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return Task.FromResult(priority);
        }

        lock (_lock)
        {
            if (_routingRules.TryGetValue(interfaceName, out var rules))
            {
                // Find matching rule with highest priority
                var matchingRule = rules
                    .Where(r => r.Condition(record, messageProperties))
                    .OrderByDescending(r => r.Priority)
                    .FirstOrDefault();

                if (matchingRule != null)
                {
                    priority = matchingRule.Priority;
                }
            }
        }

        return Task.FromResult(priority);
    }

    public Task<string> RouteMessageAsync(
        string interfaceName,
        Dictionary<string, string> record,
        Dictionary<string, object> messageProperties,
        CancellationToken cancellationToken = default)
    {
        // Default topic name
        var topicName = $"interface-{interfaceName.ToLowerInvariant()}";

        lock (_lock)
        {
            if (_routingRules.TryGetValue(interfaceName, out var rules))
            {
                // Find matching rule with highest priority
                var matchingRule = rules
                    .Where(r => r.Condition(record, messageProperties))
                    .OrderByDescending(r => r.Priority)
                    .FirstOrDefault();

                if (matchingRule != null && !string.IsNullOrWhiteSpace(matchingRule.TopicName))
                {
                    topicName = matchingRule.TopicName;
                    
                    // Add additional properties from rule
                    foreach (var prop in matchingRule.AdditionalProperties)
                    {
                        messageProperties[prop.Key] = prop.Value;
                    }

                    _logger?.LogDebug(
                        "Message routed using rule '{RuleName}' to topic '{TopicName}'",
                        matchingRule.Name, topicName);
                }
            }
        }

        return Task.FromResult(topicName);
    }

    public Task AddRoutingRuleAsync(RoutingRule rule, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rule.InterfaceName))
        {
            throw new ArgumentException("InterfaceName is required", nameof(rule));
        }

        lock (_lock)
        {
            if (!_routingRules.TryGetValue(rule.InterfaceName, out var rules))
            {
                rules = new List<RoutingRule>();
                _routingRules[rule.InterfaceName] = rules;
            }

            // Remove existing rule with same name
            rules.RemoveAll(r => r.Name == rule.Name);
            
            // Add new rule
            rules.Add(rule);

            _logger?.LogInformation(
                "Added routing rule '{RuleName}' for interface '{InterfaceName}' with priority {Priority}",
                rule.Name, rule.InterfaceName, rule.Priority);
        }

        return Task.CompletedTask;
    }

    public Task<List<RoutingRule>> GetRoutingRulesAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_routingRules.TryGetValue(interfaceName, out var rules))
            {
                return Task.FromResult(new List<RoutingRule>(rules));
            }
        }

        return Task.FromResult(new List<RoutingRule>());
    }
}

