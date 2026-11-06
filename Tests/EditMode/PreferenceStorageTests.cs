using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dreamy.Debugger.Preferences;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class PreferenceStorageTests
    {
        private string directory,path;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"dreamy-prefs-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"prefs.json");}
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test]public void StorageReplacesAtomicallyAndRetainsBoundedBackup()
        {
            var store=new PreferenceFileStore(path);store.WriteAsync("first").GetAwaiter().GetResult();store.WriteAsync("second").GetAwaiter().GetResult();
            var candidates=store.ReadAsync().GetAwaiter().GetResult();Assert.That(candidates,Is.EqualTo(new[]{"second","first"}));Assert.That(File.Exists(path+".tmp"),Is.False);
        }
        [Test]public void ConcurrentOwnersCannotOverwriteLatestIntent()
        {
            var first=new PreferenceFileStore(path);var second=new PreferenceFileStore(path);
            var writes=Enumerable.Range(0,80).Select(i=>(i%2==0?first:second).WriteAsync(i.ToString())).ToArray();Task.WaitAll(writes);
            Assert.That(File.ReadAllText(path),Is.EqualTo("79"));
        }
        [Test]public void OversizedInputIsIgnoredAndCannotGrowBackup()
        {
            File.WriteAllText(path,new string('x',100000));var store=new PreferenceFileStore(path);
            Assert.That(store.ReadAsync().GetAwaiter().GetResult()[0],Is.Null);
            Assert.Throws<ArgumentException>(()=>store.WriteAsync(new string('x',32769)));
            store.WriteAsync("safe").GetAwaiter().GetResult();Assert.That(new FileInfo(path+".bak").Length,Is.LessThanOrEqualTo(32768));
        }
        [Test]public void AllowlistRejectsGameStateAndSecretsAndBoundsEntries()
        {
            Assert.That(PreferenceSnapshot.Allowed("Credential"),Is.False);Assert.That(PreferenceSnapshot.Allowed("coins"),Is.False);Assert.That(PreferenceSnapshot.Allowed("relay.token"),Is.False);
            var snapshot=new PreferenceSnapshot{entries=Enumerable.Range(0,200).Select(i=>new PreferenceEntry("pin."+i,"1")).ToArray()};Assert.That(snapshot.ValidEntries().Count,Is.EqualTo(160));
        }
    }
}
