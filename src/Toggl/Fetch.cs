using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.TogglTrack.TogglApi
{
	/// <summary>
	/// This is a custom JsonConverter that ensures that certain fields are never ignored, even if they are null.
	/// 
	/// This is necessary as certain fields (ie project_id) still carry meaning when they are null, rather than being necessarily 'optional'.
	/// </summary>
	public class NullPropertyConverter : JsonConverter<object>
	{
		private readonly string[] _neverIgnore;

		public NullPropertyConverter(string[] neverIgnore)
		{
			this._neverIgnore = neverIgnore;
		}

		public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			throw new NotImplementedException();
		}

		public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
		{
			var type = value?.GetType();
			var properties = type?.GetProperties();

			if (properties is null)
			{
				return;
			}

			writer.WriteStartObject();

			foreach (var property in properties)
			{
				if (!property.CanRead)
				{
					continue;
				}

				var propertyValue = property.GetValue(value);

				if ((propertyValue is null) && (!this._neverIgnore.Contains(property.Name)))
				{
					continue;
				}

				writer.WritePropertyName(property.Name);
				JsonSerializer.Serialize(writer, propertyValue);
			}

			writer.WriteEndObject();
		}
	}

	public class AuthenticatedFetch
	{
		private readonly static JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
		{
			Converters = {
				new NullPropertyConverter(neverIgnore: new string[] {
					"project_id",
				}),
			}
		};

		private string _token;
		private readonly string _baseUrl;
		private readonly string? _paginationHeader;
		private HttpClient _httpClient;

		public int? nextPaginationCursor = null;

		private static string Base64Encode(string str)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
		}

		public AuthenticatedFetch(string token, string baseUrl, string? paginationHeader = null)
		{
			this._token = token;
			this._baseUrl = baseUrl;
			this._paginationHeader = paginationHeader;

			this._httpClient = this.CreateHttpClient();
		}

		public void UpdateToken(string token)
		{
			this._token = token;

			this._httpClient = this.CreateHttpClient();
		}

		private HttpClient CreateHttpClient()
		{
			if (this._httpClient is not null)
			{
				this._httpClient.Dispose();
			}

			return new HttpClient
			{
				BaseAddress = new Uri(this._baseUrl),
				DefaultRequestHeaders =
				{
					Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Base64Encode(this._token + ":api_token")),
				},
			};
		}

		public async Task<T?> Get<T>(string endpoint)
		{
			var response = await this._httpClient.GetAsync(endpoint);

			if (!response.IsSuccessStatusCode)
			{
				return default(T);
			}

			return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
		}

		public async Task<T?> Post<T>(string endpoint, object body)
		{
			var json = JsonSerializer.Serialize(body, AuthenticatedFetch._serializerOptions);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await this._httpClient.PostAsync(endpoint, content);

			if (!response.IsSuccessStatusCode)
			{
				return default(T);
			}

			if (!string.IsNullOrWhiteSpace(this._paginationHeader) && response.Headers.TryGetValues(this._paginationHeader, out var nextPaginationCursor))
			{
				try
				{
					this.nextPaginationCursor = int.Parse(nextPaginationCursor.FirstOrDefault() ?? "");
				}
				catch
				{
					this.nextPaginationCursor = null;
				}
			}
			else
			{
				this.nextPaginationCursor = null;
			}

			return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync());
		}

		public async Task<T?> Patch<T>(string endpoint, object body)
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

		public async Task<T?> Put<T>(string endpoint, object body)
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

		public async Task<HttpStatusCode?> Delete<T>(string endpoint)
		{
			var response = await this._httpClient.DeleteAsync(endpoint);

			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			return response.StatusCode;
		}
	}
}