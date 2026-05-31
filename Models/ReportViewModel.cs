using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LoanClaimsManagement.Models
{
    public class ReportViewModel
    {
        [Display(Name = "Status")]
        public int? FilterStatusID { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date From")]
        public DateTime? FilterDateFrom { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date To")]
        public DateTime? FilterDateTo { get; set; }

        [Display(Name = "Loan Officer")]
        public int? FilterOfficerID { get; set; }

        // Data lists for filters
        public List<ClaimStatusItem> Statuses { get; set; } = new();
        public List<OfficerItem> Officers { get; set; } = new();

        // Query results
        public List<ClaimViewModel> MatchingClaims { get; set; } = new();

        // Aggregated dashboard metrics
        public int TotalClaimsCount { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
    }

    public class ClaimStatusItem
    {
        public int StatusID { get; set; }
        public string StatusName { get; set; } = string.Empty;
    }

    public class OfficerItem
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}
