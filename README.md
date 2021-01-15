# SqlBackupTools

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=SqlBackupTools&metric=alert_status&token=fa520b4aec4ae90222fcbad94b24e2144fcd5641)](https://sonarcloud.io/dashboard?id=SqlBackupTools)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=SqlBackupTools&metric=coverage&token=fa520b4aec4ae90222fcbad94b24e2144fcd5641)](https://sonarcloud.io/dashboard?id=SqlBackupTools)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=SqlBackupTools&metric=sqale_rating&token=fa520b4aec4ae90222fcbad94b24e2144fcd5641)](https://sonarcloud.io/dashboard?id=SqlBackupTools)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=SqlBackupTools&metric=reliability_rating&token=fa520b4aec4ae90222fcbad94b24e2144fcd5641)](https://sonarcloud.io/dashboard?id=SqlBackupTools)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=SqlBackupTools&metric=security_rating&token=fa520b4aec4ae90222fcbad94b24e2144fcd5641)](https://sonarcloud.io/dashboard?id=SqlBackupTools)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=SqlBackupTools&metric=sqale_index&token=fa520b4aec4ae90222fcbad94b24e2144fcd5641)](https://sonarcloud.io/dashboard?id=SqlBackupTools)

You need fast SQL Server backup & restore ?

SqlBackupTools will parallelize `BACKUP RESTORE` commands, and let you saturate your disk iops to achieve really fast recoveries.

This tool is used internally at Lucca to achieve low RPO log-shipping, while providing real time monitoring of all backup / restore operations.

Born initially to workaround limitations of the excellent BrentOzar's `sp_AllNightLog` code, SqlBackupTools will evolve to become the unique entry point for all backup & restore operations.

## Features

- [x] Fast parallel restore operation
- [x] Native restore methods + BrentOzar's stored procedure support
- [x] Built upon Ola Hallengren backup convention & directory structure
- [x] Mail notifications
- [x] Slack notifications

## Getting started

### Basic usage

If you just want to restore backups, and leave databases in `ONLINE` state

```cmd
SqlBackupTools.exe restore -h localhost -f \\NetworkShare\Backups\SQL-01
```

### Slack notifications

SqlBackupTools provides convenient slack notifications, letting you know when some warnings are triggered.

Those warnings include :

- RPO target miss
- Deleted / missing backups
- Technical problems

![Slack notifications](https://user-images.githubusercontent.com/5228175/104184261-7771f180-5413-11eb-8044-f4b7690e79c6.png)

### Advanced usages

Main options :

```text
  -h, --hostname                  Required. SQL Server hostname
  -f, --folder                    Required. Root backup folder
  -l, --login                     SQL Server login (leave blank for integrated security)
  -p, --password                  SQL Server password (leave blank for integrated security)
  -t, --threads                   Number of parallel threads
  --rpoLimit                      Limit in minutes before creating a RPO warning
  --timeout                       SQL Command timeout in seconds
  --logs                          Log folder
```

Filters + behavior toggles :

```text
  --fullOnly                      Restore only FULL backups. If false, then everything is restored (FULL+LOG)
  --databaseName                  Filter on specific databases
  --ignoreDatabases               Exclude specific databases from all the process
  --continueLogs                  Whether or not you are continuing to restore logs after the database has already been restored without recovering it
  --runRecovery                   Whether or not to recover the database (RESTORE DATABASE WITH RECOVERY so that it is now usable)
  --reverseOrder                  Start with small databases instead of bigger ones
```

Support for BrentOzar's stored proc :

```text
  -b, --brentozar                 Brentozar mode (use
sp_DatabaseRestore.sql script)
  --IgnoreAlreadyPresentInMsdb    Ignore LOG backups already present in msdb
```

Post-restore script execution :

```text
  --postScripts                   Sql post scripts to execute
  --postScriptFilter              Execute script on databases starting with postScriptFilter
```

Advanced restore tuning : (use with caution)

```text
  --maxTransferSize               RESTORE MAXTRANSFERSIZE option. Maximum value : 4194304
  --bufferCount                   RESTORE BUFFERCOUNT. Don't try too high values
  --noChecksum                    RESTORE NO_CHECKSUM. Ignore checksum while restoring.
```

Mail notifications :

```text
  --smtp                          Smtp server to send email
  --email                         Email address to send email
```

Slack notifications :

```text
  --slackSecret                   Slack token
  --slackChannel                  Slack channel
  --slackOnlyOnError              Send slack message only on warning or error
```

## Backup architecture best practices

### Best practices

- Separate disk between SQL data and backups
- Even better, use separate machines
- Use the fastest disk available in terms of iops
- Setup monitor and alerts on all operations

### The 3-2-1 Backup Rule

This strategy promoted by [US CERT](https://us-cert.cisa.gov/sites/default/files/publications/data_backup_options.pdf) is simple :

- Keep 3 copies of any important file: 1 primary and 2 backups.
- Keep the files on 2 different media types to protect against different types of hazards.
- Store 1 copy offsite (e.g., outside your home or business facility).

At LuccaSoftware, we have the following architecture

- Primary production datacenter :
  - Primary SQL Instance (continuous backup)
  - Backup file instance
  - Replica SQL Instance (continuous restore)
- Secondary production datacenter (offsite):
  - Replica SQL Instance (continuous restore)
- Off-network storage (offsite):
  - Blob storage on azure.

### When SQL availability group aren't enough

At LuccaSoftware, we have a database-per-tenant tenancy strategy, and we are managing more than 1000 databases per cluster. SQL Server Availability groups aren't made for such high database count, so we currently achieve our RPO target by using a simple but efficient simple LOG shipping.

## Roadmap

- [ ] Backup operations
- [ ] Restore DIFF support
- [ ] Blob storage storage provider
- [ ] Windows Service & continuous operations
