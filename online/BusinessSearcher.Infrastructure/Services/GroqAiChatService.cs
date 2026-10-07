using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessSearcher.Application.Commons.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessSearcher.Infrastructure.Services
{
    /// <summary>
    /// Proveedor de chat con LLM usando GroqCloud (endpoint compatible con OpenAI).
    /// La API key se lee de config `Groq:ApiKey` (user-secrets / variable de entorno
    /// `Groq__ApiKey`) y jamás se expone al cliente.
    /// </summary>
    public class GroqAiChatService : IAiChatService
    {
        private readonly HttpClient _http;
        private readonly ILogger<GroqAiChatService> _logger;
        private readonly string? _apiKey;
        private readonly string  _model;

        private static readonly JsonSerializerOptions _json = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GroqAiChatService(HttpClient http, IConfiguration config, ILogger<GroqAiChatService> logger)
        {
            _http   = http;
            _logger = logger;
            _apiKey = config["Groq:ApiKey"];
            // "llama-3.3-70b-versatile" (el default anterior) fue descontinuado por Groq el
            // 17/06/2026 — desde entonces toda llamada fallaba con 400 "model_decommissioned" y
            // el servicio degradaba en silencio al mensaje "no disponible" (ver catch de abajo).
            // openai/gpt-oss-120b es el reemplazo recomendado por Groq.
            _model  = config["Groq:Model"] ?? "openai/gpt-oss-120b";

            var baseUrl = config["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            _http.BaseAddress = new Uri(baseUrl);
            _http.Timeout     = TimeSpan.FromSeconds(60);
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_apiKey) && !_apiKey!.Contains("CHANGE_ME");

        public async Task<string> CompleteAsync(
            string systemPrompt, IReadOnlyList<AiMessage> conversation, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return "El asistente de IA no está configurado en el servidor todavía.";

            var messages = new List<object>(conversation.Count + 1)
            {
                new { role = "system", content = systemPrompt }
            };
            foreach (var m in conversation)
                messages.Add(new { role = m.Role, content = m.Content });

            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(new
                {
                    model       = _model,
                    messages,
                    temperature = 0.4,
                    max_tokens  = 700
                }, options: _json)
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            try
            {
                var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Groq respondió {Status}: {Body}", (int)response.StatusCode, err);
                    return "El asistente no está disponible en este momento. Intenta de nuevo en unos minutos.";
                }

                var payload = await response.Content.ReadFromJsonAsync<GroqResponse>(_json, ct);
                var text = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
                return string.IsNullOrWhiteSpace(text)
                    ? "No pude generar una respuesta. Reformula tu pregunta, por favor."
                    : text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error llamando a GroqCloud");
                return "El asistente no está disponible en este momento. Intenta de nuevo en unos minutos.";
            }
        }

        // ── DTOs de la respuesta de Groq (OpenAI-compatible) ──────────────────────
        private sealed class GroqResponse
        {
            [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
        }
        private sealed class Choice
        {
            [JsonPropertyName("message")] public ChoiceMessage? Message { get; set; }
        }
        private sealed class ChoiceMessage
        {
            [JsonPropertyName("content")] public string? Content { get; set; }
        }
    }
}
