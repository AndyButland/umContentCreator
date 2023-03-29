﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Serialization;
using Umbraco.Extensions;
using umContentCreator.Core.Interfaces;
using umContentCreator.Core.PropertyEditors;
using static Umbraco.Cms.Core.Constants.PropertyEditors.Aliases;

namespace umContentCreator.Core.Services;

public class UmContentCreatorInjectorService : IUmContentCreatorInjectorService
{
    private readonly IDataTypeService _dataTypeService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IDataValueEditorFactory _editorFactory;

    public UmContentCreatorInjectorService(IDataTypeService dataTypeService, IShortStringHelper shortStringHelper, IDataValueEditorFactory editorFactory, IContentTypeService contentTypeService)
    {
        _dataTypeService = dataTypeService;
        _shortStringHelper = shortStringHelper;
        _editorFactory = editorFactory;
        _contentTypeService = contentTypeService;
    }

    private Dictionary<string, List<IContentType>> GetAllContentTypesAndAncestors(IEnumerable<IContentType> contentTypes)
    {
        var contentTypesAndAncestors = new Dictionary<string, List<IContentType>>();

        foreach (var contentType in contentTypes)
        {
            var ancestors = _contentTypeService.GetComposedOf(contentType.Id).ToList();
            contentTypesAndAncestors.Add(contentType.Alias, ancestors);
        }

        return contentTypesAndAncestors;
    }

   public void AddUmContentCreatorToExistingContentTypes(IEnumerable<IContentType> contentTypes)
{
    const string propertyEditorAlias = "umContentCreator";
    const string propertyName = "Content Creator";
    const string propertyTabName = "Content Creator";

    var dataType = GetDataType(propertyEditorAlias);
    var contentTypesAndAncestors = GetAllContentTypesAndAncestors(contentTypes);
    
    foreach (var contentType in contentTypes)
    {
        if (!CheckIfContentCanBeUpdated(contentType, contentTypesAndAncestors, propertyEditorAlias)
            && !CheckIfNestedContentHasTextProperties(contentTypes, contentType))
        {
            continue;
        }

        var contentCreatorPropertyGroup = GetContentCreatorPropertyGroup(contentType, propertyTabName);
        var contentCreatorPropertyType = new PropertyType(_shortStringHelper, dataType, propertyEditorAlias)
        {
            Name = propertyName,
            Description = "Automatically added Content Creator property",
            Mandatory = false
        };

        contentCreatorPropertyGroup.PropertyTypes?.Add(contentCreatorPropertyType);
    }
}

   private static bool CheckIfContentCanBeUpdated(IContentTypeBase contentType, IReadOnlyDictionary<string, List<IContentType>> contentTypesAndAncestors,
       string propertyEditorAlias)
   {
       var hasTextProperties =
           contentType.PropertyTypes.Any(pt => pt.PropertyEditorAlias is TinyMce or TextArea or TextBox);

       var ancestors = contentTypesAndAncestors[contentType.Alias];
       var umContentCreatorExists = contentType.PropertyTypeExists(propertyEditorAlias) ||
                                    ancestors.Any(ct => ct.PropertyTypeExists(propertyEditorAlias));

       return !umContentCreatorExists && hasTextProperties;
   }

   private bool CheckIfNestedContentHasTextProperties(IEnumerable<IContentType> contentTypes, IContentType contentType)
   {
       var nestedContentHasTextProperties = false;
       var nestedContentProperties = contentType.PropertyTypes
           .Where(pt => pt.PropertyEditorAlias == NestedContent).ToList();

       if (!nestedContentProperties.Any())
       {
           return false;
       }

       foreach (var nestedContentProperty in nestedContentProperties)
       {
           var nestedContentDataType = _dataTypeService.GetDataType(nestedContentProperty.DataTypeId);
           var nestedContentConfig = nestedContentDataType.ConfigurationAs<NestedContentConfiguration>();

           foreach (var contentTypeInNestedContent in nestedContentConfig.ContentTypes)
           {
               var nestedContentType = _contentTypeService.Get(contentTypeInNestedContent.Alias);

               if (nestedContentType == null)
               {
                   continue;
               }

               if (nestedContentType.PropertyTypes.Any(pt => pt.PropertyEditorAlias is TinyMce or TextArea or TextBox))
               {
                   nestedContentHasTextProperties = true;
                   break;
               }
           }

           if (nestedContentHasTextProperties)
           {
               break;
           }
       }

       return nestedContentHasTextProperties;
   }


   private IDataType GetDataType(string propertyEditorAlias)
    {
        var dataType = _dataTypeService.GetByEditorAlias(propertyEditorAlias).FirstOrDefault();
        if (dataType != null)
        {
            return dataType;
        }
        
        var editor = new UmContentCreator(_editorFactory);
        dataType = new DataType(editor, new ConfigurationEditorJsonSerializer())
        {
            Name = "umContentCreator"
        };
        _dataTypeService.Save(dataType);

        return dataType;
    }
    
    private PropertyGroup GetContentCreatorPropertyGroup(IContentTypeBase contentType, string propertyTabName)
    {
        var contentCreatorPropertyGroup = contentType.PropertyGroups.FirstOrDefault(x => x.Name == propertyTabName);

        if (contentCreatorPropertyGroup != null)
        {
            return contentCreatorPropertyGroup;
        }
        
        contentCreatorPropertyGroup = new PropertyGroup(new PropertyTypeCollection(true))
        {
            Name = propertyTabName,
            Alias = propertyTabName.ToSafeAlias(_shortStringHelper),
            SortOrder = 9999
        };
        contentType.PropertyGroups.Add(contentCreatorPropertyGroup);

        return contentCreatorPropertyGroup;
    }
}