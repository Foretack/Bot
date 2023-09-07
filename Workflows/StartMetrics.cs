﻿using Bot.Enums;
using Bot.Interfaces;
using Bot.Utils;

namespace Bot.Workflows;

public class StartMetrics: IWorkflow
{
    private readonly List<IMetric> _metrics = new();
    private BackgroundTimer _metricCollector = default!;

    public ValueTask<WorkflowState> Run()
    {
        LoadMetrics();
        _metricCollector = new(TimeSpan.FromSeconds(15), async () =>
        {
            foreach (IMetric metric in _metrics) await metric.Report();
        });

        _metricCollector.Start();
        return ValueTask.FromResult(WorkflowState.Completed);
    }

    private void LoadMetrics()
    {
        Type interfaceType = typeof(IMetric);
        foreach (Type type in interfaceType.Assembly.GetTypes().Where(interfaceType.IsAssignableFrom))
        {
            if (type.IsInterface || type.IsAbstract ||
                Activator.CreateInstance(type) is not IMetric metric) continue;

            _metrics.Add(metric);
            Debug("Loaded metric: {CommandName}", metric.GetType().Name);
        }

        Information("{MetricCount} metrics are being collected", _metrics.Count);
    }
}