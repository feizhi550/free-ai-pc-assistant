using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AIPCAssistant.Models;

namespace AIPCAssistant.Services
{
    public class RollbackManager
    {
        private readonly ILogger<RollbackManager> _logger;
        private readonly string _isolationDirectory;
        private readonly string _backupDirectory;

        public RollbackManager(ILogger<RollbackManager> logger)
        {
            _logger = logger;
            _isolationDirectory = Path.Combine(Path.GetTempPath(), "AIPC_Isolation");
            _backupDirectory = Path.Combine(Path.GetTempPath(), "AIPC_Backup");
            InitializeDirectories();
        }

        private void InitializeDirectories()
        {
            try
            {
                if (!Directory.Exists(_isolationDirectory))
                {
                    Directory.CreateDirectory(_isolationDirectory);
                }

                if (!Directory.Exists(_backupDirectory))
                {
                    Directory.CreateDirectory(_backupDirectory);
                }

                _logger.LogInformation("Rollback directories initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing rollback directories");
            }
        }

        public string MoveFileToIsolation(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File does not exist: {FilePath}", filePath);
                    return null;
                }

                var fileName = Path.GetFileName(filePath);
                var isolationPath = Path.Combine(_isolationDirectory, $"{Guid.NewGuid()}_{fileName}");

                var isolationRecord = new IsolationRecord
                {
                    OriginalPath = filePath,
                    IsolationPath = isolationPath,
                    Timestamp = DateTime.Now,
                    FileSize = new FileInfo(filePath).Length
                };

                File.Move(filePath, isolationPath);
                SaveIsolationRecord(isolationRecord);

                _logger.LogInformation("File moved to isolation: {OriginalPath} -> {IsolationPath}", filePath, isolationPath);
                return isolationPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file to isolation: {FilePath}", filePath);
                return null;
            }
        }

        public bool RestoreFileFromIsolation(string isolationPath)
        {
            try
            {
                if (!File.Exists(isolationPath))
                {
                    _logger.LogWarning("Isolation file does not exist: {IsolationPath}", isolationPath);
                    return false;
                }

                var record = FindIsolationRecord(isolationPath);
                if (record == null)
                {
                    _logger.LogWarning("Isolation record not found: {IsolationPath}", isolationPath);
                    return false;
                }

                var targetDirectory = Path.GetDirectoryName(record.OriginalPath);
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Move(isolationPath, record.OriginalPath, true);
                DeleteIsolationRecord(isolationPath);

                _logger.LogInformation("File restored from isolation: {IsolationPath} -> {OriginalPath}", isolationPath, record.OriginalPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring file from isolation: {IsolationPath}", isolationPath);
                return false;
            }
        }

        public string BackupRegistryKey(string keyPath)
        {
            try
            {
                var backupFileName = $"registry_{Guid.NewGuid()}.reg";
                var backupPath = Path.Combine(_backupDirectory, backupFileName);

                var backupRecord = new RegistryBackupRecord
                {
                    KeyPath = keyPath,
                    BackupPath = backupPath,
                    Timestamp = DateTime.Now
                };

                SaveRegistryBackupRecord(backupRecord);

                _logger.LogInformation("Registry key backed up: {KeyPath} -> {BackupPath}", keyPath, backupPath);
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up registry key: {KeyPath}", keyPath);
                return null;
            }
        }

        public void CleanupIsolation(int retentionDays = 7)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var records = GetIsolationRecords();

                foreach (var record in records)
                {
                    if (record.Timestamp < cutoffDate)
                    {
                        if (File.Exists(record.IsolationPath))
                        {
                            File.Delete(record.IsolationPath);
                            _logger.LogInformation("Expired isolation file deleted: {IsolationPath}", record.IsolationPath);
                        }
                        DeleteIsolationRecord(record.IsolationPath);
                    }
                }

                _logger.LogInformation("Isolation cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up isolation");
            }
        }

        private void SaveIsolationRecord(IsolationRecord record)
        {
            try
            {
                var records = GetIsolationRecords();
                records.Add(record);
                var json = JsonSerializer.Serialize(records);
                File.WriteAllText(Path.Combine(_isolationDirectory, "isolation_records.json"), json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving isolation record");
            }
        }

        private List<IsolationRecord> GetIsolationRecords()
        {
            try
            {
                var recordsPath = Path.Combine(_isolationDirectory, "isolation_records.json");
                if (File.Exists(recordsPath))
                {
                    var json = File.ReadAllText(recordsPath);
                    return JsonSerializer.Deserialize<List<IsolationRecord>>(json) ?? new List<IsolationRecord>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting isolation records");
            }
            return new List<IsolationRecord>();
        }

        private IsolationRecord FindIsolationRecord(string isolationPath)
        {
            var records = GetIsolationRecords();
            return records.FirstOrDefault(r => r.IsolationPath == isolationPath);
        }

        private void DeleteIsolationRecord(string isolationPath)
        {
            try
            {
                var records = GetIsolationRecords();
                records.RemoveAll(r => r.IsolationPath == isolationPath);
                var json = JsonSerializer.Serialize(records);
                File.WriteAllText(Path.Combine(_isolationDirectory, "isolation_records.json"), json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting isolation record");
            }
        }

        private void SaveRegistryBackupRecord(RegistryBackupRecord record)
        {
            try
            {
                var records = GetRegistryBackupRecords();
                records.Add(record);
                var json = JsonSerializer.Serialize(records);
                File.WriteAllText(Path.Combine(_backupDirectory, "registry_backups.json"), json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving registry backup record");
            }
        }

        private List<RegistryBackupRecord> GetRegistryBackupRecords()
        {
            try
            {
                var recordsPath = Path.Combine(_backupDirectory, "registry_backups.json");
                if (File.Exists(recordsPath))
                {
                    var json = File.ReadAllText(recordsPath);
                    return JsonSerializer.Deserialize<List<RegistryBackupRecord>>(json) ?? new List<RegistryBackupRecord>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting registry backup records");
            }
            return new List<RegistryBackupRecord>();
        }
    }

    public class IsolationRecord
    {
        public string OriginalPath { get; set; }
        public string IsolationPath { get; set; }
        public DateTime Timestamp { get; set; }
        public long FileSize { get; set; }
    }

    public class RegistryBackupRecord
    {
        public string KeyPath { get; set; }
        public string BackupPath { get; set; }
        public DateTime Timestamp { get; set; }
    }
}