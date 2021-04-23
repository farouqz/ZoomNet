using Newtonsoft.Json;

namespace ZoomNet.Models.Webhooks
{
	/// <summary>
	/// This event is triggered when a meeting registrsation has been denied.
	/// </summary>
	public class MeetingRegistrationDeniedEvent : MeetingEvent
	{
		/// <summary>
		/// Gets or sets the email address of the user who denied the registration.
		/// </summary>
		[JsonProperty(PropertyName = "operator")]
		public string Operator { get; set; }

		/// <summary>
		/// Gets or sets the user ID of the operator who denied the registration.
		/// </summary>
		[JsonProperty(PropertyName = "operator_id")]
		public string OperatorId { get; set; }
	}
}
