<template>
  <b-card header-class="header">
    <template #header>
      <h2 v-if="processDate">
        <router-link :to="{ name: 'topBidsAndDrops', params: { processDate: processDate } }" title="Trending">Trending (Week ending {{ processDate }})</router-link>
      </h2>
      <h2 v-else>
        <router-link :to="{ name: 'topBidsAndDrops' }" title="Trending">Trending</router-link>
      </h2>
    </template>
    <div v-if="!this.topBidsAndDrops" class="spinner">
      <font-awesome-icon icon="circle-notch" size="3x" spin :style="{ color: '#D6993A' }" />
    </div>
    <div v-if="this.topBidsAndDrops && this.topBidsAndDrops.length === 0">No Bids and Drops Found.</div>
    <b-tabs v-if="this.topBidsAndDrops && this.topBidsAndDrops.length > 0" pills class="top-bids-and-drops-tabs">
      <b-tab title="Top Bids" title-item-class="tab-header">
        <b-table :items="topBids" :fields="standardBidFields" :sort-by.sync="standardSortBy" :sort-desc.sync="sortDesc" bordered striped responsive class="top-bids-drops-widget-table">
          <template #cell(masterGameYear)="data">
            <masterGamePopover :master-game="data.item.masterGameYear"></masterGamePopover>
          </template>
          <template #cell(totalStandardBidAmount)="data">
            {{ data.item.totalStandardBidAmount | money(0) }}
          </template>
        </b-table>
      </b-tab>
      <b-tab title="Top Counter Picks" title-item-class="tab-header">
        <b-table :items="topCounterPicks" :fields="counterPickFields" :sort-by.sync="counterPickSortBy" :sort-desc.sync="sortDesc" bordered striped responsive class="top-bids-drops-widget-table">
          <template #cell(masterGameYear)="data">
            <masterGamePopover :master-game="data.item.masterGameYear"></masterGamePopover>
          </template>
          <template #cell(totalCounterPickBidAmount)="data">
            {{ data.item.totalCounterPickBidAmount | money(0) }}
          </template>
        </b-table>
      </b-tab>
      <b-tab title="Top Drops" title-item-class="tab-header">
        <b-table :items="topDrops" :fields="dropFields" :sort-by.sync="dropSortBy" :sort-desc.sync="sortDesc" bordered striped responsive class="top-bids-drops-widget-table">
          <template #cell(masterGameYear)="data">
            <masterGamePopover :master-game="data.item.masterGameYear"></masterGamePopover>
          </template>
        </b-table>
      </b-tab>
    </b-tabs>
  </b-card>
</template>

<script>
import MasterGamePopover from '@/components/masterGamePopover.vue';
import { orderByDescending } from '@/globalFunctions';

export default {
  components: {
    MasterGamePopover
  },
  props: {
    topBidsAndDrops: { type: Array, required: false },
    processDate: { type: String, required: false }
  },
  data() {
    return {
      standardSortBy: 'totalStandardBidCount',
      counterPickSortBy: 'totalCounterPickBidCount',
      dropSortBy: 'totalDropCount',
      sortDesc: true,
      standardBidFields: [
        { key: 'masterGameYear', label: 'Game', sortable: true, thClass: 'bg-primary' },
        { key: 'totalStandardBidCount', label: 'Bids', sortable: true, thClass: 'bg-primary' },
        { key: 'totalStandardBidAmount', label: 'Total $', sortable: true, thClass: 'bg-primary' }
      ],
      counterPickFields: [
        { key: 'masterGameYear', label: 'Game', sortable: true, thClass: 'bg-primary' },
        { key: 'totalCounterPickBidCount', label: 'Bids', sortable: true, thClass: 'bg-primary' },
        { key: 'totalCounterPickBidAmount', label: 'Total $', sortable: true, thClass: 'bg-primary' }
      ],
      dropFields: [
        { key: 'masterGameYear', label: 'Game', sortable: true, thClass: 'bg-primary' },
        { key: 'successfulDrops', label: 'Drops', sortable: true, thClass: 'bg-primary' }
      ]
    };
  },
  computed: {
    topBids() {
      let relevant = this.topBidsAndDrops.filter((x) => x.totalStandardBidCount > 0);
      return orderByDescending(relevant, (x) => x.successfulStandardBids).slice(0, 25);
    },
    topCounterPicks() {
      let relevant = this.topBidsAndDrops.filter((x) => x.totalCounterPickBidCount > 0);
      return orderByDescending(relevant, (x) => x.successfulCounterPickBids).slice(0, 25);
    },
    topDrops() {
      let relevant = this.topBidsAndDrops.filter((x) => x.totalDropCount > 0);
      return orderByDescending(relevant, (x) => x.successfulDrops).slice(0, 25);
    }
  }
};
</script>

<style scoped>
.header {
  margin-bottom: 0;
  padding-bottom: 0;
}

.header h2 {
  font-size: 25px;
  margin-bottom: 0;
  padding-bottom: 0;
}

.spinner {
  display: flex;
  justify-content: space-around;
}

.top-bids-drops-widget-table {
  max-height: 500px;
}

div >>> .tab-header {
  margin-bottom: 5px;
}

div >>> .tab-header a {
  border-radius: 0px;
  font-weight: bolder;
  color: white;
}
</style>
