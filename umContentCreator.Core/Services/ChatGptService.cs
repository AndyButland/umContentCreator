﻿using System.Net.Http.Headers;
using System.Text;
using MarkdownSharp;
using Newtonsoft.Json;
using umContentCreator.Core.Interfaces;
using umContentCreator.Core.Models;
using static Umbraco.Cms.Core.Constants.PropertyEditors.Aliases;

namespace umContentCreator.Core.Services;

public class ChatGptService : IChatGptService
{
    private readonly HttpClient _httpClient;
    private const string BaseApiUrl = "https://api.openai.com/v1/completions";
    private const string Model = "text-davinci-003";
    private readonly ISettingsService _settingsService;

    public ChatGptService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient();
    }

    public async Task<string> GenerateTextAsync(GenerateTextModel model)
    {
        var settings = await _settingsService.LoadSettingsAsync();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var requestBody = JsonConvert.SerializeObject(new
        {
            model = Model,
            prompt = model.Prompt,
            temperature = model.Temperature,
            max_tokens = model.MaxTokens
        });

        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(BaseApiUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to generate text from ChatGPT API.");
        }
        var returnedText = await GetGeneratedText(response);

        if (model.PropertyEditorAlias is TinyMce)
        {
            return new Markdown().Transform(returnedText);
        }

        return returnedText;
    }

    private static async Task<dynamic> GetGeneratedText(HttpResponseMessage response)
    {
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);
        var returnedText = responseObject.choices[0].text.ToString();
        return returnedText;
    }
}