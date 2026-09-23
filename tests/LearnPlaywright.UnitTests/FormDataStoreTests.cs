using System.Text.Json;
using System.Text.RegularExpressions;
using LearnPlaywright.Logic;

namespace LearnPlaywright.UnitTests;

/// <summary>COMP-03（FormDataStore）の単体テスト。UT-03-xx はテスト仕様書のテストID。</summary>
[TestFixture]
public class FormDataStoreTests
{
    private string _baseDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "LearnPlaywright-ut-" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_baseDirectory)) Directory.Delete(_baseDirectory, recursive: true);
    }

    private static FormDataDto Valid(string? text = "dummy", double slider = 50, string? select = "apple", string? radio = "red") =>
        new() { Text = text, Slider = slider, Select = select, Radio = radio };

    // UT-03-01: 危険な文字列を含むuserIdでも、ファイル名は常に64桁16進数+.jsonで基準ディレクトリ直下になる
    [TestCase("normal-user")]
    [TestCase("../../etc/passwd")]
    [TestCase("..\\..\\windows\\system32")]
    [TestCase("CON")]
    [TestCase("NUL")]
    [TestCase("a\0b")]
    public void MapUserIdToFilePath_ProducesSafeHashedFileName(string userId)
    {
        string path = FormDataStore.MapUserIdToFilePath(userId, _baseDirectory);
        Assert.Multiple(() =>
        {
            Assert.That(Path.GetFileName(path), Does.Match("^[0-9a-f]{64}\\.json$"));
            Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(_baseDirectory)));
        });
    }

    // UT-03-02: 極端に長いuserIdでも固定長ファイル名、同一入力には同一パス（決定的）
    [Test]
    public void MapUserIdToFilePath_IsDeterministicAndFixedLength()
    {
        string longId = new('x', 5000);
        string first = FormDataStore.MapUserIdToFilePath(longId, _baseDirectory);
        string second = FormDataStore.MapUserIdToFilePath(longId, _baseDirectory);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(Path.GetFileName(first), Has.Length.EqualTo(69));
        });
    }

    // UT-03-03: userId/baseDirectoryがnull・空文字ならArgumentException
    [TestCase(null, "dir")]
    [TestCase("", "dir")]
    [TestCase("user", null)]
    [TestCase("user", "")]
    public void MapUserIdToFilePath_InvalidArguments_Throw(string? userId, string? baseDirectory)
    {
        Assert.Throws(Is.InstanceOf<ArgumentException>(), () => FormDataStore.MapUserIdToFilePath(userId!, baseDirectory!));
    }

    // UT-03-04: 検証ルールの境界値（text 1000/1001、select空、radio null/201、slider非有限）
    [TestCase("", 0, "apple", null, true, TestName = "UT-03-04a text空文字・radio未選択は有効")]
    [TestCase(null, 0, "apple", null, false, TestName = "UT-03-04b text nullは無効")]
    [TestCase("MAX", 0, "apple", "red", true, TestName = "UT-03-04c text 1000文字は有効")]
    [TestCase("OVER", 0, "apple", "red", false, TestName = "UT-03-04d text 1001文字は無効")]
    [TestCase("t", double.NaN, "apple", "red", false, TestName = "UT-03-04e slider NaNは無効")]
    [TestCase("t", double.PositiveInfinity, "apple", "red", false, TestName = "UT-03-04f slider Infinityは無効")]
    [TestCase("t", -123.5, "apple", "red", true, TestName = "UT-03-04g slider 負の有限値は有効（範囲検証なし）")]
    [TestCase("t", 0, "", "red", false, TestName = "UT-03-04h select空文字は無効")]
    [TestCase("t", 0, null, "red", false, TestName = "UT-03-04i select nullは無効")]
    [TestCase("t", 0, "apple", "RADIO201", false, TestName = "UT-03-04j radio 201文字は無効")]
    [TestCase("t", 0, "SELECT201", "red", false, TestName = "UT-03-04k select 201文字は無効")]
    public void ValidateFormData_Boundaries(string? text, double slider, string? select, string? radio, bool expectedValid)
    {
        text = text switch { "MAX" => new string('a', 1000), "OVER" => new string('a', 1001), _ => text };
        select = select == "SELECT201" ? new string('s', 201) : select;
        radio = radio == "RADIO201" ? new string('r', 201) : radio;

        var result = FormDataStore.ValidateFormData(Valid(text, slider, select, radio));
        Assert.Multiple(() =>
        {
            Assert.That(result.Valid, Is.EqualTo(expectedValid));
            Assert.That(result.Errors, expectedValid ? Is.Empty : Is.Not.Empty);
        });
    }

    // UT-03-05: 複数違反は全件列挙される
    [Test]
    public void ValidateFormData_ReportsAllViolations()
    {
        var result = FormDataStore.ValidateFormData(Valid(text: null, slider: double.NaN, select: ""));
        Assert.That(result.Errors, Has.Count.EqualTo(3));
    }

    // UT-03-06: シリアライズはcamelCase・radio nullはJSONのnull、往復で値が一致する
    [Test]
    public void SerializeAndDeserialize_RoundTrip()
    {
        var dto = Valid(radio: null);
        string json = FormDataStore.SerializeFormDataForStorage(dto);
        var restored = FormDataStore.DeserializeFormDataFromStorage(json);
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"text\":").And.Contain("\"radio\":null"));
            Assert.That(restored.Text, Is.EqualTo(dto.Text));
            Assert.That(restored.Slider, Is.EqualTo(dto.Slider));
            Assert.That(restored.Select, Is.EqualTo(dto.Select));
            Assert.That(restored.Radio, Is.Null);
        });
    }

    // UT-03-07: 復元時の異常系（構文不正・slider型不一致）は例外、未知プロパティとradio欠落は許容
    [Test]
    public void DeserializeFormDataFromStorage_ErrorAndLenientCases()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws(Is.InstanceOf<JsonException>(), () => FormDataStore.DeserializeFormDataFromStorage("{not json"));
            Assert.Throws(Is.InstanceOf<JsonException>(), () =>
                FormDataStore.DeserializeFormDataFromStorage("{\"text\":\"a\",\"slider\":\"abc\",\"select\":\"b\"}"));
            var lenient = FormDataStore.DeserializeFormDataFromStorage("{\"text\":\"a\",\"slider\":1,\"select\":\"b\",\"extra\":true}");
            Assert.That(lenient.Radio, Is.Null);
        });
    }

    // UT-03-08: 保存→読込の往復（ディレクトリ自動作成・上書き）
    [Test]
    public void SaveThenLoad_RestoresLatestValues()
    {
        Assert.That(FormDataStore.SaveFormData("user-1", Valid(text: "first"), _baseDirectory).Success, Is.True);
        Assert.That(FormDataStore.SaveFormData("user-1", Valid(text: "second"), _baseDirectory).Success, Is.True);

        var loaded = FormDataStore.LoadFormData("user-1", _baseDirectory);
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Found, Is.True);
            Assert.That(loaded.Data!.Text, Is.EqualTo("second"));
        });
    }

    // UT-03-09: 検証エラー時はsuccess:falseでファイルを作らない
    [Test]
    public void SaveFormData_ValidationError_ReturnsFalseWithoutWriting()
    {
        var result = FormDataStore.SaveFormData("user-1", Valid(text: null), _baseDirectory);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(Directory.Exists(_baseDirectory), Is.False);
        });
    }

    // UT-03-10: 未保存ユーザーの読込はFound:false（例外なし）
    [Test]
    public void LoadFormData_NotFound_ReturnsFoundFalse()
    {
        var result = FormDataStore.LoadFormData("never-saved", _baseDirectory);
        Assert.Multiple(() =>
        {
            Assert.That(result.Found, Is.False);
            Assert.That(result.Data, Is.Null);
        });
    }

    // UT-03-11: 保存ファイルが破損している場合の読込は例外（未存在とは区別）
    [Test]
    public void LoadFormData_CorruptedFile_Throws()
    {
        string path = FormDataStore.MapUserIdToFilePath("user-1", _baseDirectory);
        FormDataStore.WriteFormDataFile(path, "{broken");
        Assert.Throws(Is.InstanceOf<JsonException>(), () => FormDataStore.LoadFormData("user-1", _baseDirectory));
    }

    // UT-03-12: Save/Loadの引数不正は例外
    [Test]
    public void SaveAndLoad_InvalidArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => FormDataStore.SaveFormData("", Valid(), _baseDirectory));
            Assert.Throws<ArgumentNullException>(() => FormDataStore.SaveFormData("u", null!, _baseDirectory));
            Assert.Throws<ArgumentException>(() => FormDataStore.SaveFormData("u", Valid(), ""));
            Assert.Throws<ArgumentException>(() => FormDataStore.LoadFormData("", _baseDirectory));
            Assert.Throws<ArgumentException>(() => FormDataStore.LoadFormData("u", ""));
        });
    }

    // UT-03-13: ファイルI/O異常は例外として伝播する（success:falseにしない）
    [Test]
    public void SaveFormData_IoError_Throws()
    {
        // baseDirectoryの位置に同名の「ファイル」を置き、ディレクトリ作成を失敗させる
        File.WriteAllText(_baseDirectory, "blocker");
        try
        {
            Assert.Throws(Is.InstanceOf<IOException>(), () => FormDataStore.SaveFormData("u", Valid(), _baseDirectory));
        }
        finally
        {
            File.Delete(_baseDirectory);
        }
    }
}
