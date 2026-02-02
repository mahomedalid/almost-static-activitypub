# Almost Static ActivityPub

Adds an activity pub simple server to a static site using dotnet azure functions and other dotnet utilities.

## Goals

* **Allow a blog to federate with Mastodon instances.** This means, that the blog may appear in other ActivityPub implementations, but the focus and priority is to follow the **Mastodon implementation** of ActivityPub.
* **Use static files whenever possible.** This allows to maintain everything cheap and fast.
* When static files are not possible, use the most cheapest alternative in Azure. Which in this case was Azure Functions. But, this can easily implemented in AWS, GCP, or custom servers.

## Features

* **Fediverse Integration** - Your blog appears as a discoverable user account in Mastodon and other ActivityPub-compatible platforms, allowing visitors to follow and subscribe to your content
* **Cross-Platform Publishing** - Blog posts are automatically distributed to Mastodon and other fediverse instances, reaching a wider audience
* **Interactive Comments** - Replies from Mastodon users appear directly on your blog posts, creating a seamless conversation experience
* **Custom Domain Support** - Uses your existing blog domain for ActivityPub identity, maintaining brand consistency

## Roadmap 

* **Quote Posts Support** - Enable the ability to quote and share posts from other ActivityPub instances
* **Content Moderation** - Implement moderation tools to filter and manage incoming replies and interactions
* **Unified Comments Dashboard** - Create a centralized page displaying all recent comments and replies across posts

## Alternatives

I found these alternatives that may be more easier/simple of implement, or are closer to what you need:

* [Adding ActivityPub to a Static Site in Vercel](https://paul.kinlan.me/adding-activity-pub-to-your-static-site/).
* [Adding comments to your Static Site with Mastodon](https://carlschwan.eu/2020/12/29/adding-comments-to-your-static-blog-with-mastodon/).

## Getting Started

Follow the instructions on [these series to implement activitypub in a static site](https://maho.dev/2024/02/a-guide-to-implement-activitypub-in-a-static-site-or-any-website/).

## License

This project is licensed under the terms described in [LICENSE.md](LICENSE.md).

