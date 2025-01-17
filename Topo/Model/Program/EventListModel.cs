﻿using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace Topo.Model.Program
{
    public class EventListModel
    {
        public string Id { get; set; } = string.Empty;
        [Display(Name = "Program Name")]
        public string EventName { get; set; } = "";

        [Display(Name = "Date")]
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }

        [Display(Name = "Challenge Area")]
        public string ChallengeArea { get; set; } = string.Empty;
        public string EventDisplay => $"{EventName} {StartDateTime.ToShortDateString()}";

        [Display(Name = "Date")]
        public string EventDate => StartDateTime.ToShortDateString();

        public List<EventAttendance> attendees = new List<EventAttendance>();

        [Display(Name = "Status")]
        public string EventStatus { get; set; } = string.Empty;
    }

    public class EventAttendance
    {
        public string id { get; set; } = "";
        public string first_name { get; set; } = "";
        public string last_name { get; set; } = "";
        public string member_number { get; set; } = "";
        public string patrol_name { get; set; } = "";
        public bool isAdultMember { get; set; }
        public bool attended { get; set; }
    }
}
