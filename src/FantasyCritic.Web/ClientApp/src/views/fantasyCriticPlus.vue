<template>
  <div class="col-md-10 offset-md-1 col-sm-12">
    <img class="plus-logo" src="@/assets/fc-plus-horizontal.svg" />
    <div class="text-well">
      <h2>What is Fantasy Critic Plus?</h2>
      <p>
        Fantasy Critic Plus is a set of premium features available to those who support us on
        <a href="https://patreon.com/fantasycritic" target="_blank">
          Patreon
          <font-awesome-icon icon="external-link-alt" size="sm" />
        </a>
        . Here are the features currently included:
      </p>

      <div class="row">
        <div class="col-lg-12 col-xl-6">
          <h3>Publisher Icons and Slogans</h3>
          <p>
            Plus users can chose an icon and slogan to represent their Publishers, both in normal leagues and in Critics Royale. Right now, the icon is limited to "one emoji", but we may add support
            for custom images in the future.
          </p>
          <h3>Share Publisher Option</h3>
          <p>Plus users can click a button to easily share their publishers (their lists of games) to social media.</p>
        </div>
        <div class="col-lg-12 col-xl-6 d-flex justify-content-center">
          <b-img src="/images/plus-publisher-example.png" fluid alt="Publisher Example"></b-img>
        </div>
      </div>
      <hr />
      <div class="row">
        <div class="col-lg-12 col-xl-6">
          <h3>Conferences</h3>
          <p>
            Plus users have the ability to form "conferences", which are made of up of more than one league. This feature is intended to be used by large groups of 20 or more users who want to all
            play Fantasy Critic together.
          </p>
          <p>
            Any user can be in a conference, but only Plus Users can form and manage a conference. For more details, check out the
            <router-link :to="{ name: 'faq', hash: '#conferences' }">Conferences</router-link>
            page.
          </p>
        </div>
      </div>
      <hr />
      <div class="row">
        <div class="col-lg-12 col-xl-6">
          <h3>Cover Art View</h3>
          <p>Plus users can view any publisher in a more visual way, as shown below. There's also a share button to easily share the image to social media.</p>
        </div>
        <div class="col-lg-12 col-xl-6 d-flex justify-content-center">
          <b-img src="/images/plus-cover-art-view-example.png" fluid alt="Cover Art View Example"></b-img>
        </div>
      </div>
      <hr />
      <div class="row">
        <div class="col-lg-12 col-xl-6">
          <h3>Advanced Searching Features</h3>
          <p>
            Plus users gain access to extra search tools on the "Master Games" page. More specifically, they will be able to see games that are available, or not available, in a given league that they
            are in.
          </p>
          <p>Players can use this to easily see all the stats for any game that is available, or see all the stats for the games that are taken, perhaps to make a decision about a counter pick.</p>
        </div>
        <div class="col-lg-12 col-xl-6 d-flex justify-content-center">
          <b-img src="/images/master-game-searching-example.png" fluid alt="Master Game Searching Example"></b-img>
        </div>
      </div>
      <hr />
      <div class="row">
        <div class="col-lg-12 col-xl-6">
          <h3>Extra League Views</h3>
          <p>In addition to being able to see "upcoming games" for a given league or across all their publishers (on the home page), plus users can also see "recent games" in the same fashion.</p>
          <h3>Extra Publisher Info</h3>
          <p>
            Plus users have access to extra features on the publisher details page. They can sort the games a publisher has by any field they like, and they can also see games that player has
            previously dropped. This even works for previous years, making it easy to go back and see how your draft fared.
          </p>
        </div>
        <div class="col-lg-12 col-xl-6 d-flex justify-content-center">
          <b-img src="/images/game-news-example.png" fluid alt="Game News Example"></b-img>
        </div>
      </div>
      <hr />
      <h2>Okay, that sounds good. What else?</h2>
      <p>
        The list of features will continue to grow over time, and we are interested in your input on what should be added. In general, features will be "quality of life" features. We'll never, for
        example, give players bonus points in their league just for being Fantasy Critic Plus users.
      </p>
    </div>
  </div>
</template>
<style scoped>
.plus-logo {
  margin-top: 10px;
  margin-bottom: 10px;
  background-color: rgba(50, 50, 50, 0.8);
  border-radius: 20px;
  max-width: 1000px;
  margin-left: auto;
  margin-right: auto;
  display: block;
}
</style>
