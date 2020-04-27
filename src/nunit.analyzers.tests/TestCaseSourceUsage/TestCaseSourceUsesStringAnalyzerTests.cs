using Gu.Roslyn.Asserts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Analyzers.Constants;
using NUnit.Analyzers.TestCaseSourceUsage;
using NUnit.Framework;

namespace NUnit.Analyzers.Tests.TestCaseSourceUsage
{
    [TestFixture]
    public sealed class TestCaseSourceUsesStringAnalyzerTests
    {
        private static readonly DiagnosticAnalyzer analyzer = new TestCaseSourceUsesStringAnalyzer();
        private static readonly ExpectedDiagnostic expectedDiagnostic = ExpectedDiagnostic.Create(AnalyzerIdentifiers.TestCaseSourceStringUsage);
        private static readonly CodeFixProvider fix = new UseNameofFix();

        [Test]
        public void AnalyzeWhenNameOfSameClass()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    public class AnalyzeWhenNameOfSameClass
    {
        static string[] Tests = new[] { ""Data"" };

        [TestCaseSource(nameof(Tests))]
        public void Test()
        {
        }
    }");
            AnalyzerAssert.Valid<TestCaseSourceUsesStringAnalyzer>(testCode);
        }

        [Test]
        public void AnalyzeWhenNameOfSameClassNotStatic()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    public class AnalyzeWhenNameOfSameClassNotStatic
    {
        string[] Tests = new[] { ""Data"" };

        [TestCaseSource(↓nameof(Tests))]
        public void Test()
        {
        }
    }");
            var expectedDiagnostic = ExpectedDiagnostic
                .Create(AnalyzerIdentifiers.TestCaseSourceSourceIsNotStatic)
                .WithMessage("Specified source 'Tests' is not static.");
            AnalyzerAssert.Diagnostics<TestCaseSourceUsesStringAnalyzer>(expectedDiagnostic, testCode);
        }

        [Test]
        public void NoWarningWhenStringLiteralMissingMember()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    public class NoWarningWhenStringLiteralMissingMember
    {
        [TestCaseSource(""Missing"")]
        public void Test()
        {
        }
    }");
            var descriptor = new DiagnosticDescriptor(AnalyzerIdentifiers.TestCaseSourceStringUsage, string.Empty, string.Empty, string.Empty, DiagnosticSeverity.Warning, true);
            AnalyzerAssert.Valid(analyzer, descriptor, testCode);
        }

        [TestCase("private static readonly TestCaseData[] TestCases = new TestCaseData[0];")]
        [TestCase("private static TestCaseData[] TestCases => new TestCaseData[0];")]
        [TestCase("private static TestCaseData[] TestCases() => new TestCaseData[0];")]
        public void FixWhenStringLiteral(string testCaseMember)
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    public class AnalyzeWhenStringConstant
    {
        private static readonly TestCaseData[] TestCases = new TestCaseData[0];

        [TestCaseSource(↓""TestCases"")]
        public void Test()
        {
        }
    }").AssertReplace("private static readonly TestCaseData[] TestCases = new TestCaseData[0];", testCaseMember);

            var fixedCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    public class AnalyzeWhenStringConstant
    {
        private static readonly TestCaseData[] TestCases = new TestCaseData[0];

        [TestCaseSource(nameof(TestCases))]
        public void Test()
        {
        }
    }").AssertReplace("private static readonly TestCaseData[] TestCases = new TestCaseData[0];", testCaseMember);

            var message = "Consider using nameof(TestCases) instead of \"TestCases\".";
            AnalyzerAssert.CodeFix(analyzer, fix, expectedDiagnostic.WithMessage(message), testCode, fixedCode);
        }

        [Test]
        public void FixWhenMultipleUnrelatedAttributes()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    [TestFixture]
    public class AnalyzeWhenMultipleUnrelatedAttributes
    {
        private static readonly TestCaseData[] TestCases = new TestCaseData[0];

        [Test]
        public void UnrelatedTest()
        {
        }

        [TestCaseSource(↓""TestCases"")]
        public void Test()
        {
        }
    }");

            var fixedCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    [TestFixture]
    public class AnalyzeWhenMultipleUnrelatedAttributes
    {
        private static readonly TestCaseData[] TestCases = new TestCaseData[0];

        [Test]
        public void UnrelatedTest()
        {
        }

        [TestCaseSource(nameof(TestCases))]
        public void Test()
        {
        }
    }");

            var message = "Consider using nameof(TestCases) instead of \"TestCases\".";
            AnalyzerAssert.CodeFix(analyzer, fix, expectedDiagnostic.WithMessage(message), testCode, fixedCode);
        }

        [Test]
        public void AnalyzeWhenNumberOfParametersMatch()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    [TestFixture]
    public class AnalyzeWhenNumberOfParametersMatch
    {
        [TestCaseSource(nameof(TestData), new object[] { 1, 3, 5 })]
        public void ShortName(int number)
        {
            Assert.That(number, Is.GreaterThanOrEqualTo(0));
        }

        static IEnumerable<int> TestData(int first, int second, int third)
        {
            yield return first;
            yield return second;
            yield return third;
        }
    }", additionalUsings: "using System.Collections.Generic;");

            AnalyzerAssert.Valid<TestCaseSourceUsesStringAnalyzer>(testCode);
        }

        [Test]
        public void AnalyzeWhenNumberOfParametersDoesNotMatchNoParametersExpected()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    [TestFixture]
    public class AnalyzeWhenNumberOfParametersDoesNotMatchNoParametersExpected
    {
        [TestCaseSource(↓nameof(TestData), new object[] { 1 })]
        public void ShortName(int number)
        {
            Assert.That(number, Is.GreaterThanOrEqualTo(0));
        }

        static IEnumerable<int> TestData()
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }
    }", additionalUsings: "using System.Collections.Generic;");

            var expectedDiagnostic = ExpectedDiagnostic
                .Create(AnalyzerIdentifiers.TestCaseSourceMismatchInNumberOfParameters)
                .WithMessage("The TestCaseSource provides '1' parameter(s), but the target method expects '0' parameter(s).");
            AnalyzerAssert.Diagnostics<TestCaseSourceUsesStringAnalyzer>(expectedDiagnostic, testCode);
        }

        [Test]
        public void AnalyzeWhenNumberOfParametersDoesNotMatchNoParametersProvided()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    [TestFixture]
    public class AnalyzeWhenNumberOfParametersDoesNotMatchNoParametersProvided
    {
        [TestCaseSource(↓nameof(TestData))]
        public void ShortName(int number)
        {
            Assert.That(number, Is.GreaterThanOrEqualTo(0));
        }

        static IEnumerable<int> TestData(string dummy)
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }
    }", additionalUsings: "using System.Collections.Generic;");

            var expectedDiagnostic = ExpectedDiagnostic
                .Create(AnalyzerIdentifiers.TestCaseSourceMismatchInNumberOfParameters)
                .WithMessage("The TestCaseSource provides '0' parameter(s), but the target method expects '1' parameter(s).");
            AnalyzerAssert.Diagnostics<TestCaseSourceUsesStringAnalyzer>(expectedDiagnostic, testCode);
        }

        [Test]
        public void AnalyzeWhenNumberOfParametersDoesNotMatch()
        {
            var testCode = TestUtility.WrapClassInNamespaceAndAddUsing(@"
    [TestFixture]
    public class AnalyzeWhenNumberOfParametersDoesNotMatchNoParametersProvided
    {
        [TestCaseSource(↓nameof(TestData), new object[] { 1, 2, 3 })]
        public void ShortName(int number)
        {
            Assert.That(number, Is.GreaterThanOrEqualTo(0));
        }

        static IEnumerable<int> TestData(string dummy, int anotherDummy)
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }
    }", additionalUsings: "using System.Collections.Generic;");

            var expectedDiagnostic = ExpectedDiagnostic
                .Create(AnalyzerIdentifiers.TestCaseSourceMismatchInNumberOfParameters)
                .WithMessage("The TestCaseSource provides '3' parameter(s), but the target method expects '2' parameter(s).");
            AnalyzerAssert.Diagnostics<TestCaseSourceUsesStringAnalyzer>(expectedDiagnostic, testCode);
        }
    }
}
