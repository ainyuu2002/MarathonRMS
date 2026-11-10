using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace MarathonRMS.API.Infrastructure;

/// <summary>
/// Thông báo kiểm tra dữ liệu tiếng Việt cho mọi DTO: attribute nào chưa ghi ErrorMessage thì gán câu mặc định tiếng Việt,
/// cùng các câu của model binding. Không phải sửa từng DTO; DTO đã có ErrorMessage riêng thì giữ nguyên.
/// (API và WebApp mỗi bên một bản vì WebApp chỉ tham chiếu DTOs.)
/// </summary>
public static class VietnameseValidationMessages
{
    public static void Apply(MvcOptions options)
    {
        options.ModelMetadataDetailsProviders.Add(new Provider());

        var m = options.ModelBindingMessageProvider;
        m.SetValueMustNotBeNullAccessor(_ => "Vui lòng nhập thông tin này.");
        m.SetMissingBindRequiredValueAccessor(_ => "Vui lòng nhập thông tin này.");
        m.SetMissingKeyOrValueAccessor(() => "Vui lòng nhập thông tin này.");
        m.SetMissingRequestBodyRequiredValueAccessor(() => "Thiếu dữ liệu gửi lên.");
        m.SetAttemptedValueIsInvalidAccessor((value, _) => $"Giá trị '{value}' không hợp lệ.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Giá trị '{value}' không hợp lệ.");
        m.SetUnknownValueIsInvalidAccessor(_ => "Giá trị không hợp lệ.");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Giá trị không hợp lệ.");
        m.SetValueIsInvalidAccessor(value => $"Giá trị '{value}' không hợp lệ.");
        m.SetValueMustBeANumberAccessor(_ => "Vui lòng nhập số.");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "Vui lòng nhập số.");
    }

    private sealed class Provider : IValidationMetadataProvider
    {
        /// <summary>[EmailAddress], [Phone], [Url] tự gán sẵn câu tiếng Anh vào ErrorMessage ⇒ coi câu đó là "chưa dịch".</summary>
        private static readonly Dictionary<Type, string?> FrameworkDefaults = new()
        {
            [typeof(EmailAddressAttribute)] = new EmailAddressAttribute().ErrorMessage,
            [typeof(PhoneAttribute)] = new PhoneAttribute().ErrorMessage,
            [typeof(UrlAttribute)] = new UrlAttribute().ErrorMessage
        };

        private static bool HasOwnMessage(ValidationAttribute attribute) =>
            attribute.ErrorMessageResourceType is not null ||
            (!string.IsNullOrEmpty(attribute.ErrorMessage) &&
             !(FrameworkDefaults.TryGetValue(attribute.GetType(), out var english) && attribute.ErrorMessage == english));

        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (HasOwnMessage(attribute)) continue;
                attribute.ErrorMessage = attribute switch
                {
                    RequiredAttribute => "Vui lòng nhập thông tin này.",
                    StringLengthAttribute { MinimumLength: > 0 } => "Độ dài từ {2} đến {1} ký tự.",
                    StringLengthAttribute or MaxLengthAttribute => "Tối đa {1} ký tự.",
                    MinLengthAttribute => "Tối thiểu {1} ký tự.",
                    RangeAttribute => "Giá trị phải từ {1} đến {2}.",
                    EmailAddressAttribute => "Email không hợp lệ.",
                    PhoneAttribute => "Số điện thoại không hợp lệ.",
                    UrlAttribute => "Đường dẫn không hợp lệ.",
                    RegularExpressionAttribute => "Sai định dạng.",
                    CompareAttribute => "Giá trị nhập lại không khớp.",
                    _ => "Giá trị không hợp lệ."
                };
            }
        }
    }
}
