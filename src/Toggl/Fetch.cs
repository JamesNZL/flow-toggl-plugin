using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.TogglTrack.TogglApi
{
	public class AuthenticatedFetch
	{
		private readonly static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
		};

		private readonly string _token;
		private readonly string _baseUrl;

		private static string Base64Encode(string str)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
		}

		public AuthenticatedFetch(string token, string baseUrl)
		{
			this._token = token;
			this._baseUrl = baseUrl;
		}

		private HttpClient GetHttpClient()
		{
			return new HttpClient
			{
				BaseAddress = new Uri(this._baseUrl),
				DefaultRequestHeaders =
				{
					Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Base64Encode(this._token + ":api_token")),
				},
			};
		}

		public async Task<T> Get<T>(string endpoint)
		{
			using var httpClient = this.GetHttpClient();
			var response = await httpClient.GetAsync(endpoint);

			if (!response.IsSuccessStatusCode)
			{
				// TODO: handle errors
				throw new Exception($"{response.StatusCode} {response.ReasonPhrase}");
			}

			return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
		}

		public async Task<T> Post<T>(string endpoint, object body)
		{
			using var httpClient = this.GetHttpClient();
			var json = JsonSerializer.Serialize(body, AuthenticatedFetch._serializerOptions);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await httpClient.PostAsync(endpoint, content);

			if (!response.IsSuccessStatusCode)
			{
				// TODO: handle errors
				throw new Exception($"{response.StatusCode} {response.ReasonPhrase}, {await response.Content.ReadAsStringAsync()}");
			}

			return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
		}

		public async Task<T> Patch<T>(string endpoint, object body)
		{
			using var httpClient = this.GetHttpClient();
			var json = JsonSerializer.Serialize(body, AuthenticatedFetch._serializerOptions);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await httpClient.PatchAsync(endpoint, content);

			if (!response.IsSuccessStatusCode)
			{
				// TODO: handle errors
				throw new Exception($"{response.StatusCode} {response.ReasonPhrase}, {await response.Content.ReadAsStringAsync()}");
			}

			return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
		}
	}
}