#!/usr/bin/env python3
"""Inventory published runtime packages and retain their exact cached public notices.

This is an offline evidence collector, not a native SBOM or license-compliance attestation.
"""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import unicodedata
import zipfile
import xml.etree.ElementTree as ET

MAX_METADATA_BYTES = 2 * 1024 * 1024
MAX_NOTICE_BYTES = 2 * 1024 * 1024
MAX_TOTAL_NOTICE_BYTES = 20 * 1024 * 1024
COMPONENTS = {'cli': 'LeviathanText.Cli', 'api': 'LeviathanText.Api', 'worker': 'LeviathanText.Worker'}


def safe_relative(value):
    path = PurePosixPath(value)
    if not value or path.is_absolute() or any(part in ('', '.', '..') for part in value.split('/')) or '\\' in value or ':' in value:
        raise ValueError('Unsafe package-relative path: ' + value)
    return Path(*path.parts)


def contained(root, relative):
    target = root / safe_relative(relative)
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Package file escapes its package directory: ' + relative)
    return target


def bounded_read(path, maximum):
    if not path.is_file() or path.stat().st_size > maximum:
        raise ValueError('Missing or oversized package evidence file: ' + path.name)
    raw = path.read_bytes()
    if len(raw) > maximum:
        raise ValueError('Package evidence file grew beyond its limit: ' + path.name)
    return raw


def is_notice(path):
    name = path.name.lower()
    return path.suffix.lower() != '.lic' and (
        name.startswith(('license', 'licence', 'notice', 'copying', 'copyright', 'third-party-notice'))
        or any(part.lower() in ('licenses', 'licences') for part in path.parts))


def collect_plan(release, cache):
    packages = {}
    manifests = []
    for component, assembly in COMPONENTS.items():
        source = release / component / (assembly + '.deps.json')
        raw = bounded_read(source, MAX_METADATA_BYTES)
        deps = json.loads(raw)
        manifests.append({'component': component, 'path': source.relative_to(release).as_posix(),
                          'sha256': hashlib.sha256(raw).hexdigest()})
        for identity, library in deps['libraries'].items():
            if library['type'] != 'package':
                continue
            name, version = identity.split('/', 1)
            if not all(re.fullmatch(r'[A-Za-z0-9_.+\-]+', value) for value in (name, version)):
                raise ValueError('Invalid package identity: ' + identity)
            expected = name.lower() + '/' + version.lower()
            relative = library.get('path', expected)
            safe_relative(relative)
            if relative.lower() != expected:
                raise ValueError('Package path does not match its identity: ' + identity)
            entry = packages.setdefault(identity.lower(), {'id': name, 'version': version, 'cacheRelativePath': relative,
                'packageContentHash': library.get('sha512'), 'components': [], 'nativeAssets': set()})
            if entry['packageContentHash'] != library.get('sha512'):
                raise ValueError('Conflicting runtime package hashes: ' + identity)
            entry['components'].append(component)
            for target in deps.get('targets', {}).values():
                assets = target.get(identity, {})
                entry['nativeAssets'].update(assets.get('native', {}))
                entry['nativeAssets'].update(path for path, metadata in assets.get('runtimeTargets', {}).items()
                                            if metadata.get('assetType') == 'native')
    if not packages:
        raise ValueError('No published runtime packages were found.')

    files = {}
    output_paths = {}
    def reserve_output(relative):
        parts = safe_relative(relative).parts
        for index in range(1, len(parts) + 1):
            prefix = PurePosixPath(*parts[:index]).as_posix()
            kind = 'file' if index == len(parts) else 'directory'
            canonical = unicodedata.normalize('NFC', prefix).casefold()
            previous = output_paths.setdefault(canonical, (prefix, kind))
            if previous != (prefix, kind):
                raise ValueError('Public evidence file/directory paths alias on common filesystems: ' + relative)
    total_bytes = 0
    for identity, entry in sorted(packages.items()):
        package = contained(cache, entry['cacheRelativePath'])
        archive_path = contained(package, entry['id'].lower() + '.' + entry['version'].lower() + '.nupkg')
        package_hash = hashlib.sha512()
        package_sha256 = hashlib.sha256()
        with archive_path.open('rb') as source:
            while block := source.read(1024 * 1024):
                package_hash.update(block)
                package_sha256.update(block)
        raw_hash = base64.b64encode(package_hash.digest()).decode('ascii')
        cached_raw_hash = bounded_read(contained(package, archive_path.name + '.sha512'), MAX_METADATA_BYTES).decode('ascii').strip()
        if raw_hash != cached_raw_hash:
            raise ValueError('Cached package archive differs from its raw SHA512 receipt: ' + identity)
        # Signed archives have distinct raw ZIP and signature-independent NuGet
        # content hashes. Do not compare those two different hash definitions.
        restored = json.loads(bounded_read(contained(package, '.nupkg.metadata'), MAX_METADATA_BYTES))
        if entry['packageContentHash'] != 'sha512-' + restored['contentHash']:
            raise ValueError('Restored NuGet content hash differs from runtime metadata: ' + identity)
        entry['packageArchiveSha512'] = raw_hash
        entry['contentHashVerification'] = 'matched-restored-metadata; canonical signed-package content hash not recomputed'
        entry['packageArchiveSha256'] = package_sha256.hexdigest()
        entry['packageArchiveBytes'] = archive_path.stat().st_size
        # Only copy files actually published in the hash-pinned NuGet archive.
        # Extra files or private license keys later placed in the cache are ignored.
        with zipfile.ZipFile(archive_path) as archive:
            published = archive.infolist()
            if len({item.filename for item in published}) != len(published):
                raise ValueError('Duplicate package archive member: ' + identity)
            specs = [item for item in published if item.filename.lower().endswith('.nuspec')]
            if len(specs) != 1:
                raise ValueError('Expected one published nuspec for ' + identity)
            def read_member(relative, limit):
                safe_relative(relative)
                item = archive.getinfo(relative)
                if item.file_size > limit or item.is_dir() or (item.external_attr >> 16) & 0o170000 == 0o120000:
                    raise ValueError('Missing, symbolic or oversized public notice: ' + relative)
                raw = archive.read(item)
                if len(raw) > limit or len(raw) != item.file_size:
                    raise ValueError('Published notice size differs from its bounded metadata: ' + relative)
                return raw
            spec_name = specs[0].filename
            spec_bytes = read_member(spec_name, MAX_METADATA_BYTES)
            metadata = ET.fromstring(spec_bytes).find('{*}metadata')
            if metadata is None or metadata.findtext('{*}id', '').lower() != entry['id'].lower() \
                    or metadata.findtext('{*}version', '').lower() != entry['version'].lower():
                raise ValueError('Published nuspec identity mismatch: ' + identity)
            license_node = metadata.find('{*}license')
            declared_license = None if license_node is None else {'type': license_node.get('type'), 'value': license_node.text or ''}
            candidates = {item.filename for item in published if not item.is_dir() and is_notice(PurePosixPath(item.filename))}
            if declared_license and declared_license['type'] == 'file':
                candidates.add(declared_license['value'])
            prefix = entry['cacheRelativePath']
            reserve_output((PurePosixPath(prefix) / safe_relative(spec_name).as_posix()).as_posix())
            planned_bytes = total_bytes
            for relative in sorted(candidates):
                reserve_output((PurePosixPath(prefix) / safe_relative(relative).as_posix()).as_posix())
                size = archive.getinfo(relative).file_size
                if size > MAX_NOTICE_BYTES:
                    raise ValueError('Oversized published notice: ' + relative)
                planned_bytes += size
                if planned_bytes > MAX_TOTAL_NOTICE_BYTES:
                    raise ValueError('Combined package notices exceed the 20-MiB limit.')
            public_notices = {relative: read_member(relative, MAX_NOTICE_BYTES) for relative in sorted(candidates)}
        entry['declaredLicense'] = declared_license
        entry['licenseUrl'] = metadata.findtext('{*}licenseUrl')
        entry['authors'] = metadata.findtext('{*}authors')
        entry['copyright'] = metadata.findtext('{*}copyright')
        repository = metadata.find('{*}repository')
        entry['repository'] = dict(repository.attrib) if repository is not None else None
        prefix = entry['cacheRelativePath']
        spec_target = (PurePosixPath(prefix) / spec_name).as_posix()
        files[spec_target] = spec_bytes
        entry['nuspec'] = {'path': spec_target, 'sha256': hashlib.sha256(spec_bytes).hexdigest()}
        notices = []
        for relative, raw in public_notices.items():
            total_bytes += len(raw)
            if total_bytes > MAX_TOTAL_NOTICE_BYTES:
                raise ValueError('Combined package notices exceed the 20-MiB limit.')
            target = (PurePosixPath(prefix) / safe_relative(relative).as_posix()).as_posix()
            files[target] = raw
            notices.append({'path': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()})
        entry['notices'] = notices
        entry['components'] = sorted(set(entry['components']))
        entry['nativeAssets'] = sorted(entry['nativeAssets'])
        entry['reviewRequired'] = ([] if notices else ['no-embedded-license-or-notice']) \
            + (['native-bundled-component-notice-review'] if entry['nativeAssets'] else [])
    inventory = {'schemaVersion': 1, 'scope': 'Published runtime NuGet packages only; framework runtime and development toolchain are separate.',
                 'attestation': False, 'nativeSbomComplete': False, 'packageSignaturesVerified': False, 'runtimeManifests': manifests,
                 'packages': [packages[key] for key in sorted(packages)], 'noticeFiles': sum(len(p['notices']) for p in packages.values()),
                 'noticeBytes': total_bytes, 'reviewRequired': [key for key in sorted(packages) if packages[key]['reviewRequired']]}
    return inventory, files


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('release', type=Path)
    parser.add_argument('output', type=Path, help='Fresh directory; existing paths are never replaced')
    parser.add_argument('--package-cache', type=Path, default=Path(os.environ.get('NUGET_PACKAGES', Path.home() / '.nuget/packages')))
    args = parser.parse_args(argv)
    if args.output.exists() or args.output.is_symlink():
        raise ValueError('Notice output already exists; choose a fresh directory.')
    inventory, files = collect_plan(args.release.resolve(), args.package_cache.resolve())
    args.output.mkdir()  # Atomic refusal if another writer creates the destination.
    for relative, raw in sorted(files.items()):
        destination = args.output / safe_relative(relative)
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open('xb') as stream:
            stream.write(raw)
    (args.output / 'README.md').write_text(
        '# Runtime third-party evidence\n\n'
        'runtime-packages.json lists the exact published runtime package graph, declared licensing metadata and copied public notice hashes. '
        'Notices are retained byte-for-byte from hash-verified published NuGet archives in the package cache. No remote terms were fetched.\n\n'
        'Raw archives are checked against their cached SHA512 receipts; NuGet content hashes match restored metadata. Signed-package canonical hashes and signatures are not reverified here.\n\n'
        'This is not a complete native-component SBOM or legal redistribution approval. '
        'Packages without embedded notices and all native bundled-component coverage require review; see reviewRequired. '
        'The framework runtime and development toolchain are outside this inventory. Dataset and model rights are separate.\n\n'
        'Dated fixed-version supplemental evidence may be packaged under docs/third-party/runtime-license-supplements/. '
        'Consult that catalog separately; it does not automatically clear the runtime inventory review flags.\n', encoding='utf8')
    with (args.output / 'runtime-packages.json').open('x', encoding='utf8') as stream:
        json.dump(inventory, stream, indent=2, ensure_ascii=False, sort_keys=True)
        stream.write('\n')
    print(json.dumps({'packages': len(inventory['packages']), 'noticeFiles': inventory['noticeFiles'],
                      'noticeBytes': inventory['noticeBytes'], 'reviewRequiredPackages': len(inventory['reviewRequired'])}))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, ET.ParseError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print('Third-party evidence collection failed: ' + str(error), file=sys.stderr)
        raise SystemExit(1)
