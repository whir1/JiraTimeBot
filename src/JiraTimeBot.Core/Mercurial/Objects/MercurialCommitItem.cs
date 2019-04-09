﻿using System;

namespace JiraTimeBot.Core.Mercurial.Objects
{
    public class MercurialCommitItem
    {
        public string Description { get; set; }
        public string Branch { get; set; }
        public DateTime Time { get; set; }
        public int FilesAffected { get; set; }
    }
}