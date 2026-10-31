using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// ذخیره وضعیت راه‌اندازی با رمزنگاری وابسته به ماشین ویندوز.
/// فایل فقط برای سروری که آن را ایجاد کرده قابل رمزگشایی است.
/// </summary>
public sealed class DpapiBootstrapStateStore : IBootstrapStateStore
{
    private static readonly byte[] Entropy =
        "FleetManagement.Bootstrap.v1"u8.ToArray();

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiBootstrapStateStore(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // مسیر پیش‌فرض در ProgramData و خارج از پوشه عمومی وب قرار دارد.
        _filePath = configuration["Bootstrap:FilePath"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "FleetManagement",
                "bootstrap.dat");

        if (!Path.IsPathRooted(_filePath))
        {
            _filePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, _filePath));
        }
    }

    /// <inheritdoc />
    public async Task<BootstrapState> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_filePath))
            {
                return new BootstrapState();
            }

            var protectedBytes = await File.ReadAllBytesAsync(_filePath, cancellationToken);

            // DPAPI با محدوده LocalMachine، فایل را به همان Windows Server مقید می‌کند.
            var clearBytes = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.LocalMachine);

            return JsonSerializer.Deserialize<BootstrapState>(clearBytes)
                ?? new BootstrapState();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        BootstrapState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("مسیر فایل راه‌اندازی معتبر نیست.");

            Directory.CreateDirectory(directory);

            var clearBytes = JsonSerializer.SerializeToUtf8Bytes(state);
            var protectedBytes = ProtectedData.Protect(
                clearBytes,
                Entropy,
                DataProtectionScope.LocalMachine);

            var temporaryPath = _filePath + ".tmp";

            await File.WriteAllBytesAsync(
                temporaryPath,
                protectedBytes,
                cancellationToken);

            // جایگزینی اتمی مانع باقی ماندن فایل نیمه‌نوشته در قطع برق می‌شود.
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
