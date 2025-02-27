<template>
  <div>
    <div class="reset-button-flex">
      <h5 class="help-text">Drag and Drop to Re-arrange</h5>
      <b-button variant="warning" class="reset-button" @click="resetValues">Reset Changes</b-button>
    </div>
    <div v-show="showWarning && !showDanger && !showPortDanger" class="alert alert-warning">You've chosen slightly non-standard settings. Be sure this is what you want.</div>
    <div v-show="showEarlyAccessWarning" class="alert alert-warning">
      The settings you've chosen will allow an early access game to be drafted/bid on, but only
      <em>before</em>
      the game enters early access. This is a reasonable choice, but be careful - it's very rare for a game to go enter early access and then see a full release within the same year. For this reason,
      allowing early access games is more of a pitfall than anything else.
    </div>
    <div v-show="showEarlyAccessDanger" class="alert alert-warning">If you ban 'Planned for Early Access' you must also ban 'Currently in Early Access'. See the FAQ page for an explanation.</div>
    <div v-show="showDanger && !showPortDanger" class="alert alert-danger">
      The settings you have selected are REALLY not recommended, unless you really know what you are doing and want a highly custom league.
    </div>
    <div v-show="showPortDanger" class="alert alert-danger">
      Please, please, don't allow the tag 'Port'. These games very very rarely get new Open Critic pages. You're free to allow the tag, but please be aware that this is an "unsupported" feature.
    </div>
    <div class="tag-flex-container">
      <div class="tag-flex-drag">
        <draggable class="tag-drag-list bg-secondary" :list="internalValue.banned" group="tags" @change="onChange">
          <div v-for="element in internalValue.banned" :key="element" class="tag-drag-item">
            <font-awesome-icon icon="bars" />
            <masterGameTagBadge :tag-name="element"></masterGameTagBadge>
          </div>
          <template #header>
            <span class="tag-header">Banned Tags</span>
          </template>
        </draggable>
      </div>

      <div class="tag-flex-drag">
        <draggable class="tag-drag-list bg-secondary" :list="internalValue.allowed" group="tags" @change="onChange">
          <div v-for="element in internalValue.allowed" :key="element" class="tag-drag-item">
            <font-awesome-icon icon="bars" />
            <masterGameTagBadge :tag-name="element"></masterGameTagBadge>
          </div>
          <template #header>
            <span class="tag-header">Allowed Tags</span>
          </template>
        </draggable>
      </div>
    </div>
  </div>
</template>

<script>
import draggable from 'vuedraggable';
import MasterGameTagBadge from '@/components/masterGameTagBadge.vue';
import { intersection } from '@/globalFunctions';

export default {
  components: {
    draggable,
    MasterGameTagBadge
  },
  props: {
    value: { type: Object, required: true },
    gameMode: { type: String, required: true }
  },
  data() {
    return {
      initialValue: {
        banned: [],
        allowed: [],
        required: []
      },
      internalValue: {
        banned: [],
        allowed: [],
        required: []
      }
    };
  },
  computed: {
    tagOptions() {
      return this.$store.getters.allTags.filter((x) => !x.systemTagOnly);
    },
    showWarning() {
      let recommendedAllowedTags = ['Reimagining'];
      let recommendedBannedTags = ['DirectorsCut', 'ReleasedInternationally', 'CurrentlyInEarlyAccess'];
      let bannedIntersection = intersection(this.internalValue.banned, recommendedAllowedTags);
      let allowedIntersection = intersection(this.internalValue.allowed, recommendedBannedTags);
      return bannedIntersection.length > 0 || allowedIntersection.length > 0;
    },
    showDanger() {
      let recommendedAllowedTags = ['NewGame', 'NewGamingFranchise', 'WillReleaseInternationallyFirst'];
      let recommendedBannedTags = ['Port'];
      let bannedIntersection = intersection(this.internalValue.banned, recommendedAllowedTags);
      let allowedIntersection = intersection(this.internalValue.allowed, recommendedBannedTags);
      return bannedIntersection.length > 0 || allowedIntersection.length > 0;
    },
    showPortDanger() {
      let recommendedAllowedTags = [];
      let recommendedBannedTags = ['Port'];
      let bannedIntersection = intersection(this.internalValue.banned, recommendedAllowedTags);
      let allowedIntersection = intersection(this.internalValue.allowed, recommendedBannedTags);
      return bannedIntersection.length > 0 || allowedIntersection.length > 0;
    },
    showEarlyAccessWarning() {
      return this.internalValue.allowed.includes('PlannedForEarlyAccess') && !this.internalValue.allowed.includes('CurrentlyInEarlyAccess');
    },
    showEarlyAccessDanger() {
      return this.internalValue.allowed.includes('CurrentlyInEarlyAccess') && !this.internalValue.allowed.includes('PlannedForEarlyAccess');
    }
  },
  created() {
    this.initializeValues();
  },
  methods: {
    onChange() {
      this.$emit('input', this.internalValue);
    },
    resetValues() {
      this.internalValue = structuredClone(this.initialValue);
    },
    initializeValues() {
      this.initialValue = {
        banned: [],
        allowed: [],
        required: []
      };

      this.internalValue = {
        banned: [],
        allowed: [],
        required: []
      };

      this.tagOptions.forEach((tag) => {
        if (this.value.banned.includes(tag.name)) {
          this.initialValue.banned.push(tag.name);
          return;
        }
        if (this.value.required.includes(tag.name)) {
          this.initialValue.required.push(tag.name);
          return;
        }

        this.initialValue.allowed.push(tag.name);
      });

      this.internalValue = structuredClone(this.initialValue);
    }
  }
};
</script>

<style>
.reset-button-flex {
  display: flex;
  justify-content: space-between;
  margin-bottom: 10px;
}

.tag-flex-container {
  display: flex;
}
.tag-flex-drag {
  flex-grow: 1;
  margin: 3px;
}
.tag-drag-list {
  border-radius: 10px;
  padding: 5px;
}
.tag-drag-item {
  margin: 10px;
  position: relative;
  display: block;
  padding: 10px 15px;
  margin-bottom: -1px;
  background-color: #5b6977 !important;
  border: 1px solid #ddd;
}
.tag-header {
  padding-left: 10px;
  font-size: 20px;
  font-weight: bold;
  color: #d6993a;
}
</style>
