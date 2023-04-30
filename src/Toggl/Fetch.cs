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

		private string _token;
		private readonly string _baseUrl;
		private HttpClient _httpClient = null;

		private static string Base64Encode(string str)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
		}

		public AuthenticatedFetch(string token, string baseUrl)
		{
			this._token = token;
			this._baseUrl = baseUrl;

			this.CreateHttpClient();
		}

		public void UpdateToken(string token)
		{
			this._token = token;

			this.CreateHttpClient();
		}

		private void CreateHttpClient()
		{
			if (this._httpClient is not null)
			{
				this._httpClient.Dispose();
			}

			this._httpClient = new HttpClient
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
			try
			{
				var response = await this._httpClient.GetAsync(endpoint);

				if (!response.IsSuccessStatusCode)
				{
					return default(T);
				}

				return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
			}
			catch (HttpRequestException exception)
			{
				throw exception;
			}
		}

		public async Task<T> Post<T>(string endpoint, object body)
		{
			try
			{
				var json = JsonSerializer.Serialize(body, AuthenticatedFetch._serializerOptions);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				var response = await this._httpClient.PostAsync(endpoint, content);

				if (!response.IsSuccessStatusCode)
				{
					return default(T);
				}

				return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
			}
			catch (HttpRequestException exception)
			{
				throw exception;
			}
		}

		public async Task<T> Patch<T>(string endpoint, object body)
		{
			try
			{
				var json = JsonSerializer.Serialize(body, AuthenticatedFetch._serializerOptions);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				var response = await this._httpClient.PatchAsync(endpoint, content);

				if (!response.IsSuccessStatusCode)
				{
					return default(T);
				}

				return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
			}
			catch (HttpRequestException exception)
			{
				throw exception;
			}
		}

		public async Task<T> Put<T>(string endpoint, object body)
		{
			try
			{
				var json = JsonSerializer.Serialize(body, AuthenticatedFetch._serializerOptions);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				var response = await this._httpClient.PutAsync(endpoint, content);

				if (!response.IsSuccessStatusCode)
				{
					return default(T);
				}

				return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
			}
			catch (HttpRequestException exception)
			{
				throw exception;
			}
		}
	}
}