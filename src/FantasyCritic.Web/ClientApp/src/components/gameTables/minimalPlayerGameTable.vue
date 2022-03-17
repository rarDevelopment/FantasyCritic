<template>
  <div :id="'publisher-' + publisher.publisherID" class="player-summary bg-secondary" v-bind:class="{ 'publisher-is-next': publisher.nextToDraft }">
    <div class="publisher-player-names">
      <div class="publisher-name-and-icon">
        <span v-if="publisher.publisherIcon && iconIsValid" class="publisher-icon">
          {{ publisher.publisherIcon }}
        </span>
        <div>
          <div class="publisher-name">
            {{ publisher.publisherName }}
          </div>
          <router-link v-show="!renderingSnapshot" :to="{ name: 'publisher', params: { publisherid: publisher.publisherID } }">
            <font-awesome-icon icon="info-circle" />
            Details
          </router-link>
        </div>
      </div>
      <div class="player-name">
        Player: {{ publisher.playerName }}
        <div v-show="!renderingSnapshot">
          <b-button variant="secondary" v-if="userIsPublisher && isPlusUser" size="sm" v-on:click="prepareSnapshot">
            <font-awesome-icon icon="share-alt" size="lg" class="share-button" />
            <span>Share</span>
          </b-button>
        </div>
      </div>
    </div>
    <table class="table table-striped">
      <thead>
        <tr class="bg-secondary">
          <th scope="col" class="game-column">Game</th>
          <th scope="col" class="score-column">Critic</th>
          <th scope="col" class="score-column">Points</th>
        </tr>
      </thead>
      <tbody>
        <minimalPlayerGameSlotRow
          v-for="gameSlot in gameSlots"
          :minimal="true"
          :gameSlot="gameSlot"
          :supportedYear="leagueYear.supportedYear"
          :hasSpecialSlots="leagueYear.hasSpecialSlots"
          :userIsPublisher="userIsPublisher"
          v-bind:key="gameSlot.overallSlotNumber"></minimalPlayerGameSlotRow>
        <tr class="minimal-game-row">
          <td id="total-description">
            <span id="total-description-text" v-if="!advancedProjections">Total Fantasy Points</span>
            <span id="total-description-text" v-else>Projected Fantasy Points</span>
          </td>
          <template v-if="!advancedProjections">
            <td id="total-column" class="bg-success" colspan="2">{{ publisher.totalFantasyPoints | score }}</td>
          </template>
          <template v-else>
            <td id="total-column" class="bg-info" colspan="2">
              <em>~{{ publisher.totalProjectedPoints | score }}</em>
            </td>
          </template>
        </tr>
      </tbody>
    </table>
  </div>
</template>
<script>
import html2canvas from 'html2canvas';
import MinimalPlayerGameSlotRow from '@/components/gameTables/minimalPlayerGameSlotRow';
import GlobalFunctions from '@/globalFunctions';

export default {
  components: {
    MinimalPlayerGameSlotRow
  },
  props: ['publisher', 'leagueYear'],
  data() {
    return {
      renderingSnapshot: false
    };
  },
  computed: {
    advancedProjections() {
      return this.$store.getters.advancedProjections;
    },
    userIsPublisher() {
      return this.$store.getters.userInfo && this.publisher.userID === this.$store.getters.userInfo.userID;
    },
    gameSlots() {
      if (!this.$store.getters.draftOrderView) {
        return this.publisher.gameSlots;
      }

      return _.orderBy(this.publisher.gameSlots, ['counterPick', 'publisherGame.timestamp'], ['asc', 'asc']);
    },
    iconIsValid() {
      return GlobalFunctions.publisherIconIsValid(this.publisher.publisherIcon);
    },
    isPlusUser() {
      return this.$store.getters.isPlusUser;
    }
  },
  methods: {
    prepareSnapshot() {
      this.renderingSnapshot = true;
      setTimeout(this.sharePublisher, 1);
    },
    sharePublisher() {
      let elementID = '#publisher-' + this.publisher.publisherID;
      html2canvas(document.querySelector(elementID)).then(async (canvas) => {
        const dataUrl = canvas.toDataURL('png');
        const blob = await (await fetch(dataUrl)).blob();
        const filesArray = [
          new File([blob], 'myPublisher.png', {
            type: blob.type,
            lastModified: new Date().getTime()
          })
        ];
        const shareData = {
          files: filesArray,
          title: 'My Fantasy Critic Publisher'
        };
        navigator.share(shareData);
      });
      this.renderingSnapshot = false;
    }
  }
};
</script>
<style scoped>
.player-summary {
  margin-bottom: 10px;
  padding: 5px;
  border: solid;
  border-width: 1px;
  padding: 0;
}

.publisher-is-next {
  border-color: #45621d;
  border-width: 5px;
  border-style: solid;
  padding: 0;
}

.publisher-is-next table thead tr.table-primary th {
  background-color: #ed9d2b;
}

.player-summary table {
  margin-bottom: 0;
}

.publisher-player-names {
  margin: 15px;
  margin-bottom: 0;
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
}

.publisher-name-and-icon {
  display: flex;
  align-items: center;
}

.publisher-name {
  font-weight: bold;
  font-size: 1.3em;
  color: white;
  word-break: break-word;
}

.publisher-icon {
  font-size: 50px;
  line-height: 50px;
  padding-bottom: 5px;
}

.player-name {
  color: white;
  font-weight: bold;
}
</style>
<style>
.player-summary table th {
  border-top: none;
}

.player-summary table td {
  border: 1px solid white;
}

.player-summary table thead tr th {
  height: 35px;
  padding: 5px;
}

.player-summary table tbody tr td {
  height: 35px;
  padding: 5px;
}

.type-column {
  width: 30px;
  text-align: center;
}
.game-column {
}

.score-column {
  width: 30px;
  text-align: center;
  font-weight: bold;
  height: 20px;
  line-height: 20px;
}

#total-description {
  vertical-align: middle;
}

#total-description-text {
  float: right;
  text-align: right;
  display: table-cell;
  vertical-align: middle;
  font-weight: bold;
  font-size: 20px;
}

#total-column {
  text-align: center;
  font-weight: bold;
  font-size: 25px;
}

.share-button {
  color: white;
}
</style>