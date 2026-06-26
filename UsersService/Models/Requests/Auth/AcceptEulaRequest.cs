using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class AcceptEulaRequest
    {
        [Required(ErrorMessage = "EULA version is required.")]
        public string Version { get; set; } = string.Empty;

        public EulaAcknowledgments? Acknowledgments { get; set; }
    }

    /// <summary>
    /// The four mandatory acknowledgments from the C-BRAIN EULA. All must be true.
    /// </summary>
    public class EulaAcknowledgments
    {
        public bool Agreement { get; set; }
        public bool PeerReview { get; set; }
        public bool DataUse { get; set; }
        public bool Liability { get; set; }
    }
}
