<template>
  <b-modal id="managerDraftGameForm" ref="managerDraftGameFormRef" size="lg" title="Select Draft Game" hide-footer @hidden="clearData">
    <div class="alert alert-info">
      This form will allow you to draft a game for another player. You can use this if you are running a draft off of one computer, for example. If you are just looking to draft your own games, you
      should use "Draft Game" under "Player Actions"
    </div>
    <div v-if="nextPublisherUp">
      <div class="form-group">
        <label for="nextPublisherUp" class="control-label">Select the next game for publisher:</label>
        <label>
          <strong>{{ nextPublisherUp.publisherName }} (Display Name: {{ nextPublisherUp.playerName }})</strong>
        </label>
      </div>
      <form method="post" class="form-horizontal" role="form" v-on:submit.prevent="searchGame">
        <label for="draftGameName" class="control-label">Game Name</label>
        <div class="input-group game-search-input">
          <input v-model="searchGameName" id="searchGameName" name="searchGameName" type="text" class="form-control input" />
          <span class="input-group-btn">
            <b-button variant="info" v-on:click="searchGame">Search Game</b-button>
          </span>
        </div>

        <div v-if="!leagueYear.hasSpecialSlots">
          <b-button variant="secondary" v-on:click="getTopGames" class="show-top-button">Show Top Available Games</b-button>
        </div>
        <div v-else>
          <h5 class="text-black">Search by Slot</h5>
          <span class="search-tags">
            <searchSlotTypeBadge :gameSlot="leagueYear.slotInfo.overallSlot" name="ALL" v-on:click.native="getTopGames"></searchSlotTypeBadge>
            <searchSlotTypeBadge :gameSlot="leagueYear.slotInfo.regularSlot" name="REG" v-on:click.native="getGamesForSlot(leagueYear.slotInfo.regularSlot)"></searchSlotTypeBadge>
            <searchSlotTypeBadge
              v-for="specialSlot in leagueYear.slotInfo.specialSlots"
              :key="specialSlot.overallSlotNumber"
              :gameSlot="specialSlot"
              v-on:click.native="getGamesForSlot(specialSlot)"></searchSlotTypeBadge>
          </span>
        </div>

        <div v-if="!draftMasterGame">
          <h3 class="text-black" v-show="showingTopAvailable">Top Available Games</h3>
          <h3 class="text-black" v-show="!showingTopAvailable && possibleMasterGames && possibleMasterGames.length > 0">Search Results</h3>
          <possibleMasterGamesTable v-if="possibleMasterGames.length > 0" v-model="draftMasterGame" :possibleGames="possibleMasterGames" v-on:input="newGameSelected"></possibleMasterGamesTable>
        </div>

        <div v-show="searched && !draftMasterGame" class="alert" v-bind:class="{ 'alert-info': possibleMasterGames.length > 0, 'alert-warning': possibleMasterGames.length === 0 }">
          <div class="row">
            <span class="col-12 col-md-7" v-show="possibleMasterGames.length > 0">Don't see the game you are looking for?</span>
            <span class="col-12 col-md-7" v-show="possibleMasterGames.length === 0">No games were found.</span>
            <b-button variant="primary" v-on:click="showUnlistedField" size="sm" class="col-12 col-md-5">Select unlisted game</b-button>
          </div>

          <div v-if="showingUnlistedField">
            <label for="draftUnlistedGame" class="control-label">Custom Game Name</label>
            <div class="input-group game-search-input">
              <input v-model="draftUnlistedGame" id="draftUnlistedGame" name="draftUnlistedGame" type="text" class="form-control input" />
            </div>
            <div>Enter the full name of the game you want.</div>
            <div>You as league manager can link this custom game with a "master game" later.</div>
          </div>
        </div>
      </form>

      <div v-if="draftMasterGame || draftUnlistedGame">
        <h3 for="draftMasterGame" v-show="draftMasterGame" class="selected-game text-black">Selected Game:</h3>
        <masterGameSummary v-if="draftMasterGame" :masterGame="draftMasterGame"></masterGameSummary>
        <hr />
        <b-button variant="primary" v-on:click="addGame" class="add-game-button" v-if="formIsValid" :disabled="isBusy">Add Game to Publisher</b-button>
        <div v-if="draftResult && !draftResult.success" class="alert draft-error" v-bind:class="{ 'alert-danger': !draftResult.overridable, 'alert-warning': draftResult.overridable }">
          <h3 class="alert-heading" v-if="draftResult.overridable">Warning!</h3>
          <h3 class="alert-heading" v-if="!draftResult.overridable">Error!</h3>
          <ul>
            <li v-for="error in draftResult.errors" :key="error">{{ error }}</li>
          </ul>

          <div class="form-check" v-if="draftResult.overridable">
            <span>
              <label class="text-white">Do you want to override these warnings?</label>
              <input class="form-check-input override-checkbox" type="checkbox" v-model="draftOverride" />
            </span>
          </div>
        </div>
      </div>
    </div>
  </b-modal>
</template>

<script>
import axios from 'axios';
import PossibleMasterGamesTable from '@/components/possibleMasterGamesTable';
import MasterGameSummary from '@/components/masterGameSummary';
import SearchSlotTypeBadge from '@/components/gameTables/searchSlotTypeBadge';

export default {
  data() {
    return {
      searchGameName: null,
      draftUnlistedGame: null,
      draftMasterGame: null,
      draftResult: null,
      draftOverride: false,
      possibleMasterGames: [],
      searched: false,
      showingUnlistedField: false,
      showingTopAvailable: false,
      isBusy: false
    };
  },
  components: {
    PossibleMasterGamesTable,
    MasterGameSummary,
    SearchSlotTypeBadge
  },
  computed: {
    formIsValid() {
      return this.draftUnlistedGame || this.draftMasterGame;
    }
  },
  props: ['leagueYear', 'nextPublisherUp', 'year'],
  methods: {
    searchGame() {
      this.clearDataExceptSearch();
      this.isBusy = true;
      axios
        .get('/api/league/PossibleMasterGames?gameName=' + this.searchGameName + '&year=' + this.year + '&leagueid=' + this.nextPublisherUp.leagueID)
        .then((response) => {
          this.possibleMasterGames = response.data;
          this.isBusy = false;
          this.searched = true;
        })
        .catch(() => {
          this.isBusy = false;
        });
    },
    getTopGames() {
      this.clearDataExceptSearch();
      this.isBusy = true;
      axios
        .get('/api/league/TopAvailableGames?year=' + this.year + '&leagueid=' + this.nextPublisherUp.leagueID)
        .then((response) => {
          this.possibleMasterGames = response.data;
          this.isBusy = false;
          this.showingTopAvailable = true;
        })
        .catch(() => {
          this.isBusy = false;
        });
    },
    getGamesForSlot(slotInfo) {
      this.clearDataExceptSearch();
      this.isBusy = true;
      let slotJSON = JSON.stringify(slotInfo);
      let base64Slot = btoa(slotJSON);
      let urlEncodedSlot = encodeURI(base64Slot);
      axios
        .get('/api/league/TopAvailableGames?year=' + this.leagueYear.year + '&leagueid=' + this.leagueYear.leagueID + '&slotInfo=' + urlEncodedSlot)
        .then((response) => {
          this.possibleMasterGames = response.data;
          this.isBusy = false;
          this.showingTopAvailable = true;
        })
        .catch(() => {
          this.isBusy = false;
        });
    },
    showUnlistedField() {
      this.showingUnlistedField = true;
      this.draftUnlistedGame = this.searchGameName;
    },
    addGame() {
      this.isBusy = true;
      var gameName = '';
      if (this.draftMasterGame !== null) {
        gameName = this.draftMasterGame.gameName;
      } else if (this.draftUnlistedGame !== null) {
        gameName = this.draftUnlistedGame;
      }

      var masterGameID = null;
      if (this.draftMasterGame !== null) {
        masterGameID = this.draftMasterGame.masterGameID;
      }

      var request = {
        publisherID: this.nextPublisherUp.publisherID,
        gameName: gameName,
        counterPick: false,
        masterGameID: masterGameID,
        managerOverride: this.draftOverride
      };

      axios
        .post('/api/leagueManager/ManagerDraftGame', request)
        .then((response) => {
          this.draftResult = response.data;
          if (!this.draftResult.success) {
            this.isBusy = false;
            return;
          }
          this.$refs.managerDraftGameFormRef.hide();
          var draftInfo = {
            gameName,
            publisherName: this.nextPublisherUp.publisherName
          };
          this.$emit('gameDrafted', draftInfo);
          this.clearData();
        })
        .catch(() => {});
    },
    clearDataExceptSearch() {
      this.isBusy = false;
      this.draftUnlistedGame = null;
      this.draftMasterGame = null;
      this.draftResult = null;
      this.draftOverride = false;
      this.possibleMasterGames = [];
      this.searched = false;
      this.showingUnlistedField = false;
      this.claimCounterPick = false;
      this.claimPublisher = null;
      this.showingTopAvailable = false;
    },
    clearData() {
      this.clearDataExceptSearch();
      this.searchGameName = null;
    },
    newGameSelected() {
      this.draftResult = null;
    }
  }
};
</script>
<style scoped>
.add-game-button {
  width: 100%;
}
.draft-error {
  margin-top: 10px;
}
.game-search-input {
  margin-bottom: 15px;
}
.override-checkbox {
  margin-left: 10px;
  margin-top: 8px;
}
.show-top-button {
  margin-bottom: 10px;
}

.search-tags {
  display: flex;
  padding: 5px;
  background: rgba(50, 50, 50, 0.7);
  border-radius: 5px;
  justify-content: space-around;
}
</style>