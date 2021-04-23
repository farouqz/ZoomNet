using Newtonsoft.Json;

namespace ZoomNet.Models
{
	/// <summary>
	/// Sharing and recording details of participants from live or past meetings.
	/// </summary>
	public class ParticipantSharingDetails : SharingDetails
	{
		/// <summary>
		/// Gets or sets the sharing details.
		/// </summary>
		/// <value>
		/// An array of sharing and recording details.
		/// </value>
		[JsonProperty(PropertyName = "details")]
		public SharingAndRecordingDetail[] SharingAndRecordingDetails { get; set; }
	}
}
