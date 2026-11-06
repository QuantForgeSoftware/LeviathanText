// Copyright (c) 2026 QuantForgeSoftware. All rights reserved.
// Generated, offline compatibility regression for the bounded Angular CLI security overrides.
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const {createRequire} = require('node:module');
const os = require('node:os');
(async () => {
  const args = process.argv.slice(2);
  let packageRoot = path.resolve(__dirname, '../web');
  let resultsDirectory;
  let positional = false;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--results-dir') {
      if (resultsDirectory || !args[i + 1] || args[i + 1].startsWith('-')) throw new Error('Expected one fresh --results-dir path');
      resultsDirectory = path.resolve(args[++i]);
    } else if (args[i].startsWith('-') || positional) {
      throw new Error('Usage: verify-npm-consumers.cjs [web-root] [--results-dir NEW_DIRECTORY]');
    } else { packageRoot = path.resolve(args[i]); positional = true; }
  }
  const req = createRequire(path.join(packageRoot, 'package.json'));
  const manifest = JSON.parse(await fs.readFile(path.join(packageRoot, 'package.json'), 'utf8'));
  assert(manifest.devDependencies?.['@angular/cli'], 'Expected the Angular web package');
  // Deliberately require the actual hoisted CLI dependencies, not substitute test doubles.
  // A changed npm layout must fail explicitly so this compatibility proof is revisited.
  const installed = name => {
    assert.equal(req.resolve(name + '/package.json'), path.join(packageRoot, 'node_modules', name, 'package.json'), 'CLI dependency layout changed: ' + name);
    return req(name);
  };
  for (const name of [ '@npmcli/package-json', 'ignore-walk', 'pacote', 'tar', 'cacache', '@tufjs/models']) installed(name);
  if (resultsDirectory) await fs.mkdir(resultsDirectory); // EEXIST refuses overwrite, including symlinks.
  const root = await fs.mkdtemp(path.join(resultsDirectory ?? os.tmpdir(), 'leviathan-npm-consumers-'));
  try {
    for (const [name, text] of Object.entries({
      'package.json': JSON.stringify({name:'generated-security-smoke', version:'1.0.0', directories:{bin:'bin'}}),
      '.npmignore':'ignored/**\n*.tmp\n!keep.tmp\n', 'README.md':'fixture', 'binding.gyp':'{}',
      'bin/greek':'fixture', 'texts/αβ.txt':'fixture', 'ignored/drop.txt':'fixture', 'drop.tmp':'fixture', 'keep.tmp':'fixture',
    })) { await fs.mkdir(path.dirname(path.join(root,name)), {recursive:true}); await fs.writeFile(path.join(root,name),text); }
    const PackageJson = req('@npmcli/package-json');
    const pkg = await PackageJson.prepare(root);
    assert.equal(pkg.content.bin.greek, 'bin/greek');
    assert.equal(pkg.content.scripts.install, 'node-gyp rebuild');
    assert.equal(pkg.content.readme,'fixture');
    const walked = (await req('ignore-walk')({path:root,ignoreFiles:['.npmignore']})).sort();
    assert(walked.includes('texts/αβ.txt')); assert(walked.includes('keep.tmp'));
    assert(!walked.includes('drop.tmp')); assert(!walked.includes('ignored/drop.txt'));
    const packed = await req('pacote').tarball('file:'+root, {ignoreScripts:true,cache:path.join(root,'cache'),tree:{path:root,package:pkg.content,isProjectRoot:true,edgesOut:new Map()}});
    assert(packed.length > 0);
    const tarPath = path.join(root,'generated.tgz'); await fs.writeFile(tarPath, packed);
    const packedPaths = []; await req('tar').t({file:tarPath,onReadEntry:entry=>packedPaths.push(entry.path)});
    assert(packedPaths.includes('package/texts/αβ.txt')); assert(packedPaths.includes('package/keep.tmp'));
    assert(!packedPaths.includes('package/drop.tmp')); assert(!packedPaths.includes('package/ignored/drop.txt'));
    const cache = req('cacache'); const cacheDir = path.join(root,'cache-direct');
    await cache.put(cacheDir,'greek','αβ');
    const verified = await cache.verify(cacheDir);
    assert.equal(verified.verifiedContent,1);
    await cache.rm.all(cacheDir);
    const {DelegatedRole} = req('@tufjs/models/dist/role.js');
    const role = new DelegatedRole({name:'fixture',keyIDs:['fixture'],threshold:1,terminating:false,paths:['texts/*.txt']});
    const delegated = ['texts/αβ.txt','texts/αβ.tmp','outside/αβ.txt','texts/nested/αβ.txt'].map(x=>role.isDelegatedPath(x));
    assert.deepEqual(delegated,[true,false,false,false]);
    const result = {node:process.version,bin:pkg.content.bin,install:pkg.content.scripts.install,readme:pkg.content.readme,walked,packedPaths:packedPaths.sort(),cacheVerified:verified.verifiedContent,delegated};
    if (resultsDirectory) await fs.writeFile(path.join(resultsDirectory, 'result.json'), JSON.stringify(result, null, 2) + '\n', {flag:'wx'});
    console.log(JSON.stringify(result));
  } finally {
    // Only the directory created by mkdtemp above is removed; caller paths are never removed.
    if (!resultsDirectory) await fs.rm(root, {recursive:true, force:true});
  }
})().catch(error=>{console.error(error);process.exitCode=1;});
