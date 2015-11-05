// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
#if DNX451
using Moq;
#endif
using Xunit;

namespace Microsoft.AspNet.Mvc.ModelBinding.Validation
{
    public class DataAnnotationsModelValidatorProviderTest
    {
        private readonly IModelMetadataProvider _metadataProvider = TestModelMetadataProvider.CreateDefaultProvider();

#if DNX451
        [Fact]
        public void GetValidators_ReturnsValidatorForIValidatableObject()
        {
            // Arrange
            var provider = new DataAnnotationsModelValidatorProvider(
                new TestOptionsManager<MvcDataAnnotationsLocalizationOptions>(),
                stringLocalizerFactory: null);
            var mockValidatable = Mock.Of<IValidatableObject>();
            var metadata = _metadataProvider.GetMetadataForType(mockValidatable.GetType());

            var providerContext = new ModelValidatorProviderContext(metadata);

            // Act
            provider.GetValidators(providerContext);

            // Assert
            var validator = Assert.Single(providerContext.Validators);
            Assert.IsType<ValidatableObjectAdapter>(validator);
        }
#endif

        [Fact]
        public void GetValidators_InsertsRequiredValidatorsFirst()
        {
            var provider = new DataAnnotationsModelValidatorProvider(
                new TestOptionsManager<MvcDataAnnotationsLocalizationOptions>(),
                stringLocalizerFactory: null);
            var metadata = _metadataProvider.GetMetadataForProperty(
                typeof(ClassWithProperty),
                "PropertyWithMultipleValidationAttributes");

            var providerContext = new ModelValidatorProviderContext(metadata);

            // Act
            provider.GetValidators(providerContext);

            // Assert
            Assert.Equal(4, providerContext.Validators.Count);
            Assert.IsType<CustomRequiredAttribute2>((providerContext.Validators[0] as DataAnnotationsModelValidator).Attribute);
            Assert.IsType<CustomRequiredAttribute1>((providerContext.Validators[1] as DataAnnotationsModelValidator).Attribute);
            Assert.IsType<CustomNonRequiredAttribute1>((providerContext.Validators[2] as DataAnnotationsModelValidator).Attribute);
            Assert.IsType<CustomNonRequiredAttribute2>((providerContext.Validators[3] as DataAnnotationsModelValidator).Attribute);
        }

        [Fact]
        public void UnknownValidationAttributeGetsDefaultAdapter()
        {
            // Arrange
            var provider = new DataAnnotationsModelValidatorProvider(
                new TestOptionsManager<MvcDataAnnotationsLocalizationOptions>(),
                stringLocalizerFactory: null);
            var metadata = _metadataProvider.GetMetadataForType(typeof(DummyClassWithDummyValidationAttribute));

            var providerContext = new ModelValidatorProviderContext(metadata);

            // Act
            provider.GetValidators(providerContext);

            // Assert
            var validator = providerContext.Validators.Single();
            Assert.IsType<DataAnnotationsModelValidator>(validator);
        }

        private class DummyValidationAttribute : ValidationAttribute
        {
        }

        [DummyValidation]
        private class DummyClassWithDummyValidationAttribute
        {
        }

        // Default IValidatableObject adapter factory

#if DNX451
        [Fact]
        public void IValidatableObjectGetsAValidator()
        {
            // Arrange
            var provider = new DataAnnotationsModelValidatorProvider(
                new TestOptionsManager<MvcDataAnnotationsLocalizationOptions>(),
                stringLocalizerFactory: null);
            var mockValidatable = new Mock<IValidatableObject>();
            var metadata = _metadataProvider.GetMetadataForType(mockValidatable.Object.GetType());

            var providerContext = new ModelValidatorProviderContext(metadata);

            // Act
            provider.GetValidators(providerContext);

            // Assert
            Assert.Single(providerContext.Validators);
        }
#endif

        private class ObservableModel
        {
            private bool _propertyWasRead;

            public string TheProperty
            {
                get
                {
                    _propertyWasRead = true;
                    return "Hello";
                }
            }

            public bool PropertyWasRead()
            {
                return _propertyWasRead;
            }
        }

        private class BaseModel
        {
            public virtual string MyProperty { get; set; }
        }

        private class DerivedModel : BaseModel
        {
            [StringLength(10)]
            public override string MyProperty
            {
                get { return base.MyProperty; }
                set { base.MyProperty = value; }
            }
        }

        private class DummyRequiredAttributeHelperClass
        {
            [Required(ErrorMessage = "Custom Required Message")]
            public int WithAttribute { get; set; }

            public int WithoutAttribute { get; set; }
        }

        private class ClassWithProperty
        {
            [CustomNonRequiredAttribute1]
            [CustomNonRequiredAttribute2]
            [CustomRequiredAttribute1]
            [CustomRequiredAttribute2]
            public string PropertyWithMultipleValidationAttributes { get; set; }
        }

        public class CustomRequiredAttribute1 : RequiredAttribute
        {
        }

        public class CustomRequiredAttribute2 : RequiredAttribute
        {
        }

        public class CustomNonRequiredAttribute1 : ValidationAttribute
        {
        }

        public class CustomNonRequiredAttribute2 : ValidationAttribute
        {
        }
    }
}
