from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from xml.etree import ElementTree as ET

import argparse
import asyncio

import aiofiles
import aiohttp
from defusedxml.ElementTree import fromstring, tostring


def get_package_name(package_reference: ET.Element) -> str | None:
    return package_reference.attrib.get("Include", None)


async def get_package_version(session: aiohttp.ClientSession, package_name: str) -> str:
    async with session.get(
        f"https://api.nuget.org/v3-flatcontainer/{package_name}/index.json",
    ) as response:
        data = await response.json()
        # Return the last version without preview tag
        for version in data["versions"][::-1]:
            if (
                "preview" not in version
                and "dev" not in version
                and "beta" not in version
                and "alpha" not in version
                and "rc" not in version
            ):
                return str(version)
        return str(data["versions"][-1])


async def update_package_version(
    session: aiohttp.ClientSession, package_reference: ET.Element,
) -> None:
    package_name = package_reference.attrib.get("Include", None)
    if not package_name:
        return
    new_version = await get_package_version(session, package_name)
    package_reference.attrib["Version"] = new_version


async def update_all_packages(session: aiohttp.ClientSession, file_path: str) -> None:
    async with aiofiles.open(file_path, mode="rb+") as f:
        content = await f.read()
        root = fromstring(content)
        async with asyncio.TaskGroup() as group:
            for package_reference in root.findall(".//PackageReference"):
                group.create_task(update_package_version(session, package_reference))
        await f.seek(0)
        await f.write(tostring(root, encoding="utf-8"))


async def main() -> None:
    # Get the arguments
    parser = argparse.ArgumentParser(description="Update all packages in project files")
    parser.add_argument(
        "project_files", help="The project files to update, separated by commas",
    )
    args = parser.parse_args()
    async with aiohttp.ClientSession() as session, asyncio.TaskGroup() as group:
        for project_file in args.project_files.split(","):
            group.create_task(update_all_packages(session, project_file))


if __name__ == "__main__":
    asyncio.run(main())
