<template>
  <span v-if="masterGame">
    <a :id="popoverID" href="javascript:;" :class="{ 'text-white': currentlyIneligible }">
      {{ masterGame.gameName }}
    </a>
    <b-popover :target="popoverID" triggers="click blur" custom-class="master-game-popover">
      <div class="mg-popover">
        <masterGameSummary :master-game="masterGame"></masterGameSummary>
      </div>
    </b-popover>
  </span>
</template>

<script>
import MasterGameSummary from '@/components/masterGameSummary.vue';

export default {
  components: {
    MasterGameSummary
  },
  props: {
    masterGame: { type: Object, required: true },
    currentlyIneligible: { type: Boolean }
  },
  computed: {
    popoverID() {
      return `mg-popover-${this._uid}`;
    }
  }
};
</script>
<style scoped>
@media only screen and (max-width: 600px) {
  .master-game-popover {
    max-width: 300px;
  }
}

@media only screen and (min-width: 601px) {
  .master-game-popover {
    max-width: 500px;
  }
}
</style>
