# Almost Static ActivityPub

Adds an activity pub simple server to a static site using dotnet azure functions and other dotnet utilities.

## Goals

* **Allow a blog to federate with Mastodon instances.** This means, that the blog may appear in other ActivityPub implementations, but the focus and priority is to follow the **Mastodon implementation** of ActivityPub.
* **Use static files whenever possible.** This allows to maintain everything cheap and fast.
* When static files are not possible, use the most cheapest alternative in Azure. Which in this case was Azure Functions. But, this can easily implemented in AWS, GCP, or custom servers.

## Features

* The blog should appear as a user in Mastodon instances, and it will allow to be followed/subscribed. **(COMPLETE)**
* Posts should appear in Mastodon instances.  **(COMPLETE)**
* Posts can be "replied" in Mastodon, and these replies would appear in my blog site.  **(COMPLETE)**
* Should use the domain of the blog. **(COMPLETE)**
* Support for multiple/accounts/tags 
* Multiple levels replies
* Generating followers json
* Allow to publish notes as threads
* Allow to pre-visualize a note

## Alternatives

I found these alternatives that may be more easier/simple of implement, or are closer to what you need:

* [Adding ActivityPub to a Static Site in Vercel](https://paul.kinlan.me/adding-activity-pub-to-your-static-site/).
* [Adding comments to your Static Site with Mastodon](https://carlschwan.eu/2020/12/29/adding-comments-to-your-static-blog-with-mastodon/).

## Design



```
