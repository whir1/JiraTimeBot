﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JiraTimeBot.Configuration;
using JiraTimeBot.Mercurial;
using JiraTimeBot.Mercurial.Objects;
using JiraTimeBot.TasksProcessors;
using JiraTimeBot.TaskTime;
using JiraTimeBot.TaskTime.Objects;

namespace JiraTimeBot
{
    class Job
    {
        private readonly IAllMercurialProviders _mercurialProviders;
        private readonly ITaskTimeCalculator _taskTimeDiscoverer;
        private readonly ILog _log;

        public Job(IAllMercurialProviders mercurialProviders, ITaskTimeCalculator taskTimeDiscoverer, ILog log)
        {
            _mercurialProviders = mercurialProviders;
            _taskTimeDiscoverer = taskTimeDiscoverer;
            _log = log;
        }

        public Task DoTheJob(Settings settings, ITasksProcessor tasksProcessor, bool dummyMode, CancellationToken cancellationToken)
        {
            _log.Info("Начинаем работу");

            return Task.Run(() => DoTheJobImpl(settings, tasksProcessor, dummyMode, cancellationToken), cancellationToken);
        }

        private void DoTheJobImpl(Settings settings, ITasksProcessor tasksProcessor, bool dummyMode, CancellationToken cancellationToken)
        {
            int daysDiff = 0;
            if (tasksProcessor is MeetingProcessor)
            {
                daysDiff = -1;
            }

            while (true)
            {
                DateTime date = DateTime.Now.Date.AddDays(daysDiff);

                IMercurialLog mercurial = _mercurialProviders.MercurialLog;
                if (settings.WorkType == WorkType.JiraLogs)
                {
                    _log.Info("Использую Jira как источник информации.");
                    mercurial = _mercurialProviders.JiraCommitEmulator;
                }
                List<MercurialCommitItem> commits = mercurial.GetMercurialLog(settings, date, cancellationToken);
                List<TaskTimeItem> taskTimes = _taskTimeDiscoverer.CalculateTaskTime(commits, settings, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!taskTimes.Any())
                {
                    _log.Warn($"{date:dd.MM.yyyy} вы не сделали ничего полезного =) Использую предыдущий день.");
                    daysDiff--;
                    if (daysDiff < -7)
                    {
                        _log.Error("Не нашли ни одного коммита за предыдущие 7 дней. Возможно вы в отпуске? Выхожу.");
                        return;
                    }

                    continue;
                }

                tasksProcessor.Process(date, taskTimes, settings, dummyMode);

                _log.Info("Готово.");
                return;
            }
        }

    }
}
