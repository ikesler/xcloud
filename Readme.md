
# XCloud - self-hosted cloud features for personal knowledge base management

Application is a REST API server which handles some automation tasks on the top of a file storage.
For now, system supports only local FS storage but the API is extensible and more storage types can be supported (e.g., S3).
It's mostly related to manipulating markdown notes, in particular [Obsidian vaults](https://help.obsidian.md/Getting+started/Create+a+vault), but some of the features work for any type of the file.
The application does not provide any file management UI, it's purely an API, with the exception of web sharing UI.

## Configuration
Application itself is configured via standard .NET options API. Next configuration providers are supported:
- JSON file
- dotenv file
- Env variables
- Hashicorp Vault

But those are only the essentials. The major part of the features are configured using `.xcloud/settings.yaml` file inside the file storage.

Here is an example of such file. Assuming `memo` directory exists in the root virtual FS folder.

```yaml
# IANA time zone code
time_zone: Australia/ACT

sharing:
  # List of folders (nesting supported) which support sharing via Obsidian URLs
  # e.g.: obsidian://open?vault=memo&file=note_file_name
  # System uses this list to parse the URLs properly
  obsidian_vaults:
    memo: memo
  # Files in these folders (any nesting level) are shared automatically if linked in a markdown document.
  # Main use case - pasted images.
  auto_shared_when_linked:
    - memo/_resources

clipper:
  read_era:
    templates_directory: memo/_templates/readera
    notes_directory: memo/📖 reading/readera
  clip_base_path: memo/✂️ clip
  bookmark_base_path: memo/🔗 links
  template_directory: memo/_templates
  epub_base_path: articles
  resources_base_path: memo/_resources
  resources_relative_path: ../_resources
  global_selectors_to_remove: []
  domain_selectors_to_remove:
    reddit.com:
      - "[slot=credit-bar]"
  domain_selectors_to_include:
    reddit.com: shreddit-post
  global_headers_to_add:
    Accept-Language: en-US,en;q=0.9
    User-Agent: Mozilla/5.0 (Android 14; Mobile; rv:129.0) Gecko/129.0 Firefox/129.0
  domain_headers_to_add:
    reddit.com:
      Cookie: ...
automations:
  - type: ai_title
    params:
      directory: memo/ongoing
```

Even more options are available for some particular features related to `.md` files inside the frontmatter.

## Features

### Web clipper

```http request
POST https://<xcloud_url>/clip/article
Content-Type: application/json
Authorization: Bearer {{$dotenv XCLOUD_TOKEN}}

{
  "url": "https://an_article.com"
}
```
Clips an article using Mozilla readability lib and saves the markdown to the folder specified in `clipper.clip_base_path` setting.

```http request
POST https://<xcloud_url>/clip/bookmark
Content-Type: application/json
Authorization: Bearer {{$dotenv XCLOUD_TOKEN}}

{
  "url": "https://an_article.com"
}
```
Same as article but using a different, short template, excluding the full article content.


```http request
POST https://<xcloud_url>/clip/epub
Content-Type: application/json
Authorization: Bearer {{$dotenv XCLOUD_TOKEN}}

{
  "url": "https://an_article.com"
}
```
Same as article but results are saved as a EPUB ebook file. Ideal for long offline reading and research work (commenting, highlighting, etc.).

### Sharing

```http request
POST https://<xcloud_url>/s
Content-Type: application/json
Authorization: Bearer {{$dotenv XCLOUD_TOKEN}}

{
  "path": "memo/a_markdown_note.md"
}
```
Shares a file and returns the public URL.
The URL will look like `https://<xcloud_url>/s/2ABF24A`
Supports a number of options inside the markdown files yaml frontmatter. In particular, protecting shares with password and nested shares.
Markdown files are rendered as HTML, including links to images.
A lightweight CSS theme based on [Cactus](https://github.com/monkeyWzr/hugo-theme-cactus) comes bundled.
Files created by Obsidian's Excalidraw integration are supported (handwriting and whiteboards).
Other file types are returned as binary file stream.

```http request
DELETE https://<xcloud_url>/s/<share_key>
Content-Type: application/json
Authorization: Bearer {{$dotenv XCLOUD_TOKEN}}
```
Deletes a previously create share.

### Readera e-book reader integration

[Readera](https://readera.org/) is a fantastic e-book reader for Android. While it provides a number of research features out of the box
(like text highlighting and adding notes) it may be extremely useful to connect that reading experience to non-ebook flows.
For example, an ebook may be a part of a bigger research and there may be a bunch of `md` notes related to it which is beyond Readera capabilities.
For that cases, a Readera backup file can be imported to the markdown notes system.

```http request
POST https://<xcloud_url>/clip/readera
Content-Type: multipart/form-data
Authorization: Bearer {{$dotenv XCLOUD_TOKEN}}

a sinle file content here
```

### Automations

#### AI Notes title
XCloud can loop over the markdown files in a folder and send them to an OpenAI-compatible API to generated file titles.
