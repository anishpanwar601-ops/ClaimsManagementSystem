using System;
using System.ComponentModel.DataAnnotations;

namespace LoanClaimsManagement.Models
{
    public class ClaimViewModel
    {
        [Display(Name = "Claim ID")]
        public int ClaimID { get; set; }

        [Required(ErrorMessage = "Loan Account Number is required.")]
        [RegularExpression(@"^[a-zA-Z0-9\-]{5,20}$", ErrorMessage = "Loan Account Number must be between 5 and 20 alphanumeric characters or hyphens.")]
        [Display(Name = "Loan Account Number")]
        public string LoanAccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Claim Type is required.")]
        [Display(Name = "Claim Type")]
        public string ClaimType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [MinLength(10, ErrorMessage = "Description must be at least 10 characters long.")]
        [Display(Name = "Claim Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Date Submitted")]
        public DateTime DateSubmitted { get; set; }

        [Display(Name = "Status ID")]
        public int StatusID { get; set; } = 1; // Default to Pending

        [Display(Name = "Status")]
        public string StatusName { get; set; } = "Pending";

        public int SubmittedByUserID { get; set; }

        [Display(Name = "Submitted By")]
        public string SubmittedByFullName { get; set; } = string.Empty;

        [Display(Name = "Submitted By User")]
        public string SubmittedByUsername { get; set; } = string.Empty;

        [Display(Name = "Reviewed By")]
        public string? ReviewedByFullName { get; set; }

        [Display(Name = "Review Date")]
        public DateTime? ReviewDate { get; set; }

        [Display(Name = "Officer Comments")]
        [StringLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters.")]
        public string? OfficerComments { get; set; }
    }
}
