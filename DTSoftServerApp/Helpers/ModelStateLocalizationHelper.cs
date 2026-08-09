using DTSoft.AppService.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DTSoftServerApp.Helpers;

public static class ModelStateLocalizationHelper
{
    public static IEnumerable<string> GetLocalizedErrors(ModelStateDictionary modelState, IAppLocalizer localizer)
    {
        return modelState.Values
            .SelectMany(v => v.Errors)
            .Select(error => Translate(localizer, error.ErrorMessage))
            .Where(message => !string.IsNullOrWhiteSpace(message));
    }

    public static string Translate(IAppLocalizer localizer, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return localizer["common.argumentMissing"];

        if (message.Contains('.') && message.IndexOf(' ') < 0)
            return localizer[message];

        return message switch
        {
            "KeyName不能为空" => localizer["integration.keyNameRequired"],
            "SecretKey不能为空" => localizer["integration.secretKeyRequired"],
            "UserAccount不能为空" => localizer["integration.userAccountRequired"],
            "ItemId不能为空" => localizer["integration.itemIdRequired"],
            "KeyName长度不能超过100个字符" => localizer["integration.keyNameMaxLength"],
            "描述长度不能超过500个字符" => localizer["integration.descriptionMaxLength"],
            "字典编码只能包含英文、数字、下划线、中划线和冒号，且以英文开头" => localizer["dictionary.codeInvalid"],
            "模型名称只能包含英文、数字和下划线，且以英文开头" => localizer["micro.modelNameInvalid"],
            "微应用路径只能包含英文、数字、中划线和下划线，且以英文开头" => localizer["micro.pathInvalid"],
            "字段标识只能包含英文、数字和下划线，且以英文开头" => localizer["micro.fieldNameInvalid"],
            "子表标识只能包含英文、数字和下划线，且以英文开头" => localizer["micro.subTableNameInvalid"],
            "连接编码只能包含英文、数字、中划线和下划线，且以英文开头" => localizer["esb.connectionCodeInvalid"],
            "数据源编码只能包含英文、数字、中划线和下划线，且以英文开头" => localizer["esb.dataSourceCodeInvalid"],
            "参数名只能包含英文、数字和下划线，且以英文开头" => localizer["esb.parameterNameInvalid"],
            _ => message
        };
    }
}
