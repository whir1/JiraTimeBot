﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Atlassian.Jira;
using JiraTimeBotForm.Configuration;
using JiraTimeBotForm.JiraIntegration.Comments;
using JiraTimeBotForm.TaskTime.Objects;

namespace JiraTimeBotForm.JiraIntegration
{
    public class JiraApi
    {
        private readonly ILog _log;
        private readonly IJiraDescriptionSource _descriptionSource;

        public JiraApi(ILog log, IJiraDescriptionSource descriptionSource)
        {
            _log = log;
            _descriptionSource = descriptionSource;
        }

        public string GetTaskName(string branch, Settings settings)
        {
            var jira = Jira.CreateRestClient(settings.JiraUrl, settings.JiraUserName, settings.JiraPassword);
            try
            {
                var issue = jira.Issues.Queryable.FirstOrDefault(f => f.Key == branch);
                return issue?.Summary;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public List<Issue> GetTodayIssues(Settings settings, DateTime? date = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var jira = Jira.CreateRestClient(settings.JiraUrl, settings.JiraUserName, settings.JiraPassword);
            date = date.GetValueOrDefault(DateTime.Now.Date);

            var jql = $"status changed by '{settings.JiraUserName}' during (\"{date.Value:yyyy-MM-dd}\",\"{date.Value.AddDays(1):yyyy-MM-dd}\")";
            var affectedIssues = jira.Issues.GetIssuesFromJqlAsync(jql, 50, 0, cancellationToken).Result.ToList();
            return affectedIssues;
        }

        public void SetTodayWorklog(List<TaskTimeItem> taskTimeItems, Settings settings, DateTime? date = null, bool dummy = false, bool addCommentsToWorklog = false)
        {
            var jira = Jira.CreateRestClient(settings.JiraUrl, settings.JiraUserName, settings.JiraPassword);

            if (date == null)
            {
                date = DateTime.Now.Date;
            }
            date = date.Value.Date;

            foreach (TaskTimeItem taskTimeItem in taskTimeItems)
            {
                Issue issue = null;
                try
                {
                    issue = jira.Issues.Queryable.FirstOrDefault(f => f.Key == taskTimeItem.Branch);
                }
                catch (Exception)
                {

                }
                if (issue == null)
                {
                    _log.Error($"[!] Не могу найти ветку {taskTimeItem.Branch} в JIRA, пропускаю!");
                    continue;
                }

                var hasTodayWorklog = false;
                var workLogs = issue.GetWorklogsAsync().Result;
                foreach (var workLog in workLogs)
                {
                    var timeSpent = TimeSpan.FromSeconds(workLog.TimeSpentInSeconds);
                    if (workLog.CreateDate.GetValueOrDefault().Date == date && workLog.Author == settings.JiraUserName)
                    {
                        var timeDiff = Math.Abs((timeSpent - taskTimeItem.Time).TotalMinutes);
                        if (timeDiff > 1)
                        {
                            _log.Trace($"Время отличается на {timeDiff} минут, удаляю worklog: {taskTimeItem.Branch} {workLog.Author} {workLog.CreateDate}: {workLog.TimeSpent}");
                            if (!dummy)
                            {
                                issue.DeleteWorklogAsync(workLog);
                            }
                            hasTodayWorklog = false;
                        }
                        else
                        {
                            _log.Trace($"По задаче {taskTimeItem.Branch} уже есть аналогичный worklog. Пропускаю.");
                            hasTodayWorklog = true;
                        }
                    }
                }

                if (!hasTodayWorklog)
                {
                    var timeSpentJira = $"{taskTimeItem.Time.TotalMinutes}m";

                    var comment = _descriptionSource.GetDescription(taskTimeItem, addCommentsToWorklog);

                    Worklog workLogToAdd = new Worklog(timeSpentJira, date.Value, comment);
                    if (!dummy)
                    {
                        workLogToAdd = issue.AddWorklogAsync(workLogToAdd).Result;
                    }
                    _log.Trace($"Добавили Worklog для {taskTimeItem.Branch}: {workLogToAdd.Author} {workLogToAdd.CreateDate}: {workLogToAdd.TimeSpent}");
                }
            }
        }
    }
}
